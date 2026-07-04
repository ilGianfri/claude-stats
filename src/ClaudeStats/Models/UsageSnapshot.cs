namespace ClaudeStats.Models;

/// <summary>Where a <see cref="UsageSnapshot"/> was read from.</summary>
public enum UsageSource
{
    /// <summary>The authenticated usage endpoint (primary).</summary>
    Endpoint,

    /// <summary>Rate-limit response headers (fallback / cross-check).</summary>
    Headers,
}

/// <summary>
/// A complete point-in-time reading of all rate-limit windows.
/// </summary>
public sealed record UsageSnapshot
{
    /// <summary>All windows parsed from the source (may be empty).</summary>
    public IReadOnlyList<LimitWindow> Windows { get; init; } = [];

    /// <summary>When this reading was captured.</summary>
    public required DateTimeOffset CapturedAt { get; init; }

    /// <summary>Provenance of this reading.</summary>
    public UsageSource Source { get; init; } = UsageSource.Endpoint;

    /// <summary>The window closest to its limit (highest percent), or null if there are none.</summary>
    public LimitWindow? MostConstraining =>
        Windows.Count == 0 ? null : Windows.MaxBy(static w => w.Percent);

    /// <summary>
    /// Derives the presentation state from the most-constraining window and the warning threshold.
    /// Freshness states (<see cref="LimitState.Stale"/>) are applied by the polling/ViewModel layer.
    /// </summary>
    /// <param name="warningThresholdPercent">Percent (0–100) at which the warning state begins.</param>
    /// <returns>The derived <see cref="LimitState"/>.</returns>
    public LimitState GetState(double warningThresholdPercent)
    {
        LimitWindow? window = MostConstraining;
        if (window is null)
        {
            return LimitState.Unavailable;
        }

        if (window.Percent >= 100.0)
        {
            return LimitState.Reached;
        }

        return window.Percent >= warningThresholdPercent ? LimitState.Warning : LimitState.Normal;
    }
}
