namespace ClaudeStats.Models;

/// <summary>
/// Aggregated usage for a single local calendar day.
/// </summary>
public sealed record DailyUsage
{
    /// <summary>The local calendar day.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Sum of input tokens.</summary>
    public long InputTokens { get; init; }

    /// <summary>Sum of output tokens.</summary>
    public long OutputTokens { get; init; }

    /// <summary>Sum of cache-creation (write) tokens.</summary>
    public long CacheCreationTokens { get; init; }

    /// <summary>Sum of cache-read tokens.</summary>
    public long CacheReadTokens { get; init; }

    /// <summary>Number of assistant messages that day.</summary>
    public int MessageCount { get; init; }

    /// <summary>Estimated cost for the day, in USD.</summary>
    public decimal EstimatedCost { get; init; }

    /// <summary>Total of all token types for the day.</summary>
    public long TotalTokens => InputTokens + OutputTokens + CacheCreationTokens + CacheReadTokens;
}
