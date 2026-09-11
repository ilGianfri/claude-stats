using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClaudeStats.Services;

/// <summary>
/// Background service that checks for a new ClaudeStats release once at startup
/// and shows a tray notification when one is found. Best-effort: any failure is logged and
/// swallowed so an unreachable GitHub can never take the host (and the tray app) down.
/// </summary>
public sealed class UpdateCheckHostedService : BackgroundService
{
    private readonly IUpdateChecker _checker;
    private readonly IShellService _shell;
    private readonly ILogger<UpdateCheckHostedService> _logger;

    /// <summary>Initializes the update check service.</summary>
    /// <param name="checker">The release checker.</param>
    /// <param name="shell">Shell service used to show the tray notification.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public UpdateCheckHostedService(
        IUpdateChecker checker,
        IShellService shell,
        ILogger<UpdateCheckHostedService> logger)
    {
        _checker = checker;
        _shell = shell;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Wait for the app to finish initializing before hitting the network.
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);

            UpdateInfo? update = await _checker.CheckAsync(stoppingToken).ConfigureAwait(false);
            if (update is not null)
            {
                _logger.LogInformation("Update available: {Version}", update.LatestVersion);
                _shell.Notify(
                    "ClaudeStats update available",
                    $"Version {update.LatestVersion} is available. Use 'Check for updates' in the tray menu.");
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed; continuing without it.");
        }
    }
}
