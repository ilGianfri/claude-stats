using ClaudeStats.Models;

namespace ClaudeStats.Services;

/// <summary>
/// Aggregates local Claude transcripts into a per-day usage-statistics roll-up.
/// </summary>
public interface IUsageStatsReader
{
    /// <summary>Aggregates usage over the last <paramref name="days"/> local calendar days.</summary>
    /// <param name="days">Number of days to include (e.g. 30).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The aggregated statistics; <see cref="UsageStatistics.IsAvailable"/> is false when no transcripts exist.</returns>
    Task<UsageStatistics> GetStatisticsAsync(int days, CancellationToken ct);
}
