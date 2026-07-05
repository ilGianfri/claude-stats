using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClaudeStats.Services;

/// <summary>
/// Background service that checks for a new ClaudeStats release once at startup
/// and shows a tray notification when one is found.
/// </summary>
public sealed class UpdateCheckHostedService : BackgroundService
{
    private readonly IUpdateChecker _checker;
    private readonly IShellService _shell;
    private readonly ILogger<UpdateCheckHostedService> _logger;

    /// <summary>Initializes the update check service.</summary>
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
}
