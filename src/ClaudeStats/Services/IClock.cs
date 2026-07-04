namespace ClaudeStats.Services;

/// <summary>
/// Abstracts the current time so time-dependent logic (reset countdowns, day bucketing,
/// token-expiry checks) stays deterministic and unit-testable.
/// </summary>
public interface IClock
{
    /// <summary>Gets the current instant, including offset from UTC.</summary>
    DateTimeOffset Now { get; }
}
