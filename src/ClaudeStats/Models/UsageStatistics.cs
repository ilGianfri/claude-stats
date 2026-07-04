namespace ClaudeStats.Models;

/// <summary>
/// Aggregated usage over a recent period (the ~30-day trend).
/// </summary>
public sealed record UsageStatistics
{
    /// <summary>Per-day aggregates, ordered ascending by date.</summary>
    public IReadOnlyList<DailyUsage> Days { get; init; } = [];

    /// <summary>Whether local transcript data was available to aggregate.</summary>
    public bool IsAvailable { get; init; }

    /// <summary>Inclusive start of the period.</summary>
    public DateOnly PeriodStart { get; init; }

    /// <summary>Inclusive end of the period.</summary>
    public DateOnly PeriodEnd { get; init; }

    /// <summary>Total tokens across the period.</summary>
    public long TotalTokens => Days.Sum(static d => d.TotalTokens);

    /// <summary>Total messages across the period.</summary>
    public int TotalMessages => Days.Sum(static d => d.MessageCount);

    /// <summary>Total estimated cost across the period, in USD.</summary>
    public decimal TotalEstimatedCost => Days.Sum(static d => d.EstimatedCost);

    /// <summary>An empty, unavailable result.</summary>
    public static UsageStatistics Unavailable { get; } = new() { IsAvailable = false };
}
