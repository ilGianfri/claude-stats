namespace ClaudeStats.Models;

/// <summary>
/// Presentation state for the most-constraining window, driving the tray icon and messaging.
/// </summary>
public enum LimitState
{
    /// <summary>Below the warning threshold.</summary>
    Normal,

    /// <summary>At or above the warning threshold (default 80%) but not yet reached.</summary>
    Warning,

    /// <summary>Fully consumed (100%).</summary>
    Reached,

    /// <summary>No usable reading available (no credentials / endpoint unreachable).</summary>
    Unavailable,

    /// <summary>Last reading is older than the staleness threshold.</summary>
    Stale,
}
