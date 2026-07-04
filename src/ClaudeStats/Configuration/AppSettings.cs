using System.Text.Json.Serialization;

namespace ClaudeStats.Configuration;

/// <summary>
/// User-configurable application settings, persisted to the app's own settings file
/// (never written into the Claude credentials/data directories).
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Poll interval in seconds. The effective interval is floored at 60s to respect the
    /// usage endpoint's rate limit (see research.md); values below 60 are clamped.
    /// </summary>
    public int RefreshIntervalSeconds { get; set; } = 60;

    /// <summary>Whether the app registers itself to launch at Windows logon.</summary>
    public bool LaunchAtStartup { get; set; }

    /// <summary>Percent (0-100) of the most-constraining window at which the warning state begins.</summary>
    public double WarningThresholdPercent { get; set; } = 80;

    /// <summary>Gets the effective refresh interval, floored at 60 seconds.</summary>
    [JsonIgnore]
    public TimeSpan RefreshInterval => TimeSpan.FromSeconds(Math.Max(60, RefreshIntervalSeconds));
}
