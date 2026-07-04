namespace ClaudeStats.Models;

/// <summary>
/// Per-model token pricing (USD per one million tokens) used to estimate cost.
/// </summary>
public sealed record ModelPrice
{
    /// <summary>Model family/id this price applies to.</summary>
    public required string ModelId { get; init; }

    /// <summary>Price per 1M input tokens.</summary>
    public decimal InputPerMTok { get; init; }

    /// <summary>Price per 1M output tokens.</summary>
    public decimal OutputPerMTok { get; init; }

    /// <summary>Price per 1M cache-write tokens.</summary>
    public decimal CacheWritePerMTok { get; init; }

    /// <summary>Price per 1M cache-read tokens.</summary>
    public decimal CacheReadPerMTok { get; init; }
}
