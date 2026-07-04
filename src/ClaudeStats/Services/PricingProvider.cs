using ClaudeStats.Models;

namespace ClaudeStats.Services;

/// <summary>
/// Bundled per-model pricing keyed by model family. Rates are approximate published USD figures
/// (per 1M tokens) as of the app's knowledge cutoff and are centralized here for easy update; the
/// resulting cost is always presented as an estimate. Matching is by family substring so specific
/// version ids (e.g. "claude-opus-4-8") resolve to their family price.
/// </summary>
public sealed class PricingProvider : IPricingProvider
{
    private static readonly ModelPrice Opus = new()
    {
        ModelId = "opus",
        InputPerMTok = 15m,
        OutputPerMTok = 75m,
        CacheWritePerMTok = 18.75m,
        CacheReadPerMTok = 1.50m,
    };

    private static readonly ModelPrice Sonnet = new()
    {
        ModelId = "sonnet",
        InputPerMTok = 3m,
        OutputPerMTok = 15m,
        CacheWritePerMTok = 3.75m,
        CacheReadPerMTok = 0.30m,
    };

    private static readonly ModelPrice Haiku = new()
    {
        ModelId = "haiku",
        InputPerMTok = 0.80m,
        OutputPerMTok = 4m,
        CacheWritePerMTok = 1.00m,
        CacheReadPerMTok = 0.08m,
    };

    /// <inheritdoc />
    public ModelPrice? TryGet(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return null;
        }

        string id = modelId.ToLowerInvariant();
        if (id.Contains("opus", StringComparison.Ordinal))
        {
            return Opus;
        }

        if (id.Contains("sonnet", StringComparison.Ordinal))
        {
            return Sonnet;
        }

        return id.Contains("haiku", StringComparison.Ordinal) ? Haiku : null;
    }
}
