namespace ClaudeStats.Models;

/// <summary>
/// A single rate-limit window at a point in time.
/// </summary>
public sealed record LimitWindow
{
    /// <summary>Which window this reading represents.</summary>
    public required WindowKind Kind { get; init; }

    /// <summary>Raw utilization from the source, expected in the range 0.0–1.0.</summary>
    public required double Utilization { get; init; }

    /// <summary>Instant at which this window resets.</summary>
    public required DateTimeOffset ResetsAt { get; init; }

    /// <summary>
    /// Optional explicit display label (used for dynamic/scoped limits). When null, the label is
    /// derived from <see cref="Kind"/>.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>Utilization expressed as a percentage (0–100), clamped defensively.</summary>
    public double Percent => Math.Clamp(Utilization, 0.0, 1.0) * 100.0;

    /// <summary>Human-readable label for the window.</summary>
    public string DisplayName => Label ?? Kind switch
    {
        WindowKind.FiveHour => "5-hour",
        WindowKind.Weekly => "Weekly",
        WindowKind.WeeklyOpus => "Weekly (Opus)",
        _ => "Other",
    };

    /// <summary>Gets the time remaining until this window resets, floored at zero.</summary>
    /// <param name="now">The current instant.</param>
    /// <returns>The non-negative time until reset.</returns>
    public TimeSpan GetTimeRemaining(DateTimeOffset now) =>
        ResetsAt > now ? ResetsAt - now : TimeSpan.Zero;
}
