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
    /// <summary>Upper bound on the exponential back-off delay applied while polls keep failing.</summary>
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(15);

    private readonly IUsageClient _client;
    private readonly IMessenger _messenger;
    private readonly IUiDispatcher _ui;
    private readonly AppSettings _settings;
    private readonly ILogger<PollingService> _logger;
    private readonly BackoffPolicy _backoff;

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
        _backoff = new BackoffPolicy(settings.RefreshInterval, MaxBackoff);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                TimeSpan delay = await PollOnceAsync(stoppingToken);
                await Task.Delay(delay, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    /// <inheritdoc />
    public async Task RefreshNowAsync(CancellationToken ct = default) => await PollOnceAsync(ct);

    /// <summary>Performs a single poll, broadcasts the outcome, and reports how long to wait next.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The delay before the next poll: the base interval after a success, or a growing back-off after a transient failure.</returns>
    private async Task<TimeSpan> PollOnceAsync(CancellationToken ct)
    {
        try
        {
            UsageSnapshot snapshot = await _client.GetUsageAsync(ct);
            _backoff.Reset();
            await _ui.InvokeAsync(() => _messenger.Send(new UsageUpdatedMessage(snapshot)));
            return _backoff.BaseInterval;
        }
        catch (UsageUnavailableException ex) when (!ex.IsTransient)
        {
            // Hard failure (e.g. sign-in required): waiting will not fix it, so stay at the base cadence.
            _backoff.Reset();
            await _ui.InvokeAsync(() => _messenger.Send(new UsageErrorMessage(LimitState.Unavailable, ex.Reason)));
            return _backoff.BaseInterval;
        }
        catch (UsageUnavailableException ex)
        {
            TimeSpan delay = _backoff.NextDelay(ex.RetryAfter);
            string reason = $"{ex.Reason} Retrying in {FormatDelay(delay)}.";
            await _ui.InvokeAsync(() => _messenger.Send(new UsageErrorMessage(LimitState.Stale, reason)));
            return delay;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while polling usage.");
            TimeSpan delay = _backoff.NextDelay();
            string reason = $"Unexpected error retrieving usage. Retrying in {FormatDelay(delay)}.";
            await _ui.InvokeAsync(() => _messenger.Send(new UsageErrorMessage(LimitState.Stale, reason)));
            return delay;
        }
    }

    /// <summary>Formats a retry delay for display (e.g. "45s" or "2m").</summary>
    /// <param name="delay">The delay to format.</param>
    /// <returns>A short, human-readable duration.</returns>
    private static string FormatDelay(TimeSpan delay) =>
        delay.TotalSeconds < 60
            ? $"{Math.Round(delay.TotalSeconds)}s"
            : $"{Math.Round(delay.TotalMinutes)}m";
}
