namespace ClaudeStats.Models;

/// <summary>
/// Identifies which rate-limit window a reading refers to.
/// </summary>
public enum WindowKind
{
    /// <summary>Unrecognized window key (defensive; excluded from the headline).</summary>
    Unknown = 0,

    /// <summary>Rolling ~5-hour window (<c>five_hour</c>).</summary>
    FiveHour,

    /// <summary>7-day / weekly window (<c>seven_day</c>).</summary>
    Weekly,

    /// <summary>Weekly Opus sub-limit (<c>seven_day_opus</c>).</summary>
    WeeklyOpus,
}
