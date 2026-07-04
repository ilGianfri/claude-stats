using ClaudeStats.Configuration;
using ClaudeStats.Messages;
using ClaudeStats.Models;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClaudeStats.Services;

/// <summary>
/// Background service that polls the usage endpoint on a fixed interval and broadcasts results,
/// marshaling all UI-facing updates onto the UI thread. Also serves on-demand refreshes.
/// </summary>
public sealed class PollingService : BackgroundService, IUsagePoller
{
    private readonly IUsageClient _client;
    private readonly IMessenger _messenger;
    private readonly IUiDispatcher _ui;
    private readonly AppSettings _settings;
    private readonly ILogger<PollingService> _logger;

    /// <summary>Initializes the polling service.</summary>
    /// <param name="client">The usage client.</param>
    /// <param name="messenger">Messenger used to broadcast updates/errors.</param>
    /// <param name="ui">UI-thread dispatcher.</param>
    /// <param name="settings">Application settings (poll interval).</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public PollingService(
        IUsageClient client,
        IMessenger messenger,
        IUiDispatcher ui,
        AppSettings settings,
        ILogger<PollingService> logger)
    {
        _client = client;
        _messenger = messenger;
        _ui = ui;
        _settings = settings;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(_settings.RefreshInterval);

        await PollOnceAsync(stoppingToken);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PollOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    /// <inheritdoc />
    public Task RefreshNowAsync(CancellationToken ct = default) => PollOnceAsync(ct);

    /// <summary>Performs a single poll and broadcasts the outcome.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the poll and broadcast finish.</returns>
    private async Task PollOnceAsync(CancellationToken ct)
    {
        try
        {
            UsageSnapshot snapshot = await _client.GetUsageAsync(ct);
            await _ui.InvokeAsync(() => _messenger.Send(new UsageUpdatedMessage(snapshot)));
        }
        catch (UsageUnavailableException ex)
        {
            LimitState state = ex.IsTransient ? LimitState.Stale : LimitState.Unavailable;
            await _ui.InvokeAsync(() => _messenger.Send(new UsageErrorMessage(state, ex.Reason)));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while polling usage.");
            await _ui.InvokeAsync(() =>
                _messenger.Send(new UsageErrorMessage(LimitState.Stale, "Unexpected error retrieving usage.")));
        }
    }
}
