using ClaudeStats.Models;

namespace ClaudeStats.Services;

/// <summary>
/// Provides per-model token pricing for cost estimation.
/// </summary>
public interface IPricingProvider
{
    /// <summary>Gets the price for a model id, or null if unknown.</summary>
    /// <param name="modelId">The model identifier (e.g. "claude-opus-4-8").</param>
    /// <returns>The matching <see cref="ModelPrice"/>, or null when unknown.</returns>
    ModelPrice? TryGet(string? modelId);
}
