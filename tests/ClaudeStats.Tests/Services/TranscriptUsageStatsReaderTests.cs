using System.Globalization;
using System.IO;
using ClaudeStats.Models;
using ClaudeStats.Services;
using ClaudeStats.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClaudeStats.Tests.Services;

/// <summary>Unit tests for <see cref="TranscriptUsageStatsReader"/> (real temp-file fixtures).</summary>
public sealed class TranscriptUsageStatsReaderTests : IDisposable
{
    private readonly FakeClock _clock = new();
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"cs-tx-{Guid.NewGuid():N}");

    private TranscriptUsageStatsReader CreateReader(string? directory = null) =>
        new(new PricingProvider(), _clock, NullLogger<TranscriptUsageStatsReader>.Instance, directory ?? _dir);

    private static string Iso(DateTimeOffset value) => value.ToString("o", CultureInfo.InvariantCulture);

    private static string Assistant(string tsIso, string model, long input, long output, long cacheWrite, long cacheRead) =>
        "{\"type\":\"assistant\",\"timestamp\":\"" + tsIso + "\",\"message\":{\"model\":\"" + model +
        "\",\"usage\":{\"input_tokens\":" + input + ",\"output_tokens\":" + output +
        ",\"cache_creation_input_tokens\":" + cacheWrite + ",\"cache_read_input_tokens\":" + cacheRead + "}}}";

    [Fact]
    public async Task GetStatisticsAsync_ReturnsUnavailable_WhenDirectoryMissing()
    {
        TranscriptUsageStatsReader reader = CreateReader(Path.Combine(_dir, "does-not-exist"));
        UsageStatistics stats = await reader.GetStatisticsAsync(30, TestContext.Current.CancellationToken);
        Assert.False(stats.IsAvailable);
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsUnavailable_WhenNoTranscriptFiles()
    {
        Directory.CreateDirectory(_dir);
        TranscriptUsageStatsReader reader = CreateReader();
        UsageStatistics stats = await reader.GetStatisticsAsync(30, TestContext.Current.CancellationToken);
        Assert.False(stats.IsAvailable);
    }

    [Fact]
    public async Task GetStatisticsAsync_AggregatesAssistantRecords_ByDay_WithinWindow()
    {
        string sub = Path.Combine(_dir, "proj1");
        Directory.CreateDirectory(sub);
        string inRange = Iso(_clock.Now.AddDays(-2));
        string outOfRange = Iso(_clock.Now.AddDays(-60));
        string future = Iso(_clock.Now.AddDays(5));

        string[] lines =
        [
            Assistant(inRange, "claude-opus-4-8", 1000, 500, 200, 100),
            Assistant(inRange, "claude-sonnet-5", 2000, 0, 0, 0),
            "{\"type\":\"user\",\"timestamp\":\"" + inRange + "\"}",     // skipped (not assistant)
            "not-json",                                                    // skipped (malformed)
            "{\"type\":\"assistant\",\"timestamp\":\"" + inRange + "\"}", // skipped (no usage)
            Assistant(outOfRange, "claude-opus-4-8", 9999, 9999, 0, 0),   // skipped (out of window)
            Assistant(future, "claude-opus-4-8", 8888, 0, 0, 0),          // skipped (future)
        ];
        await File.WriteAllLinesAsync(Path.Combine(sub, "session.jsonl"), lines, TestContext.Current.CancellationToken);

        TranscriptUsageStatsReader reader = CreateReader();
        UsageStatistics stats = await reader.GetStatisticsAsync(30, TestContext.Current.CancellationToken);

        Assert.True(stats.IsAvailable);
        DailyUsage day = Assert.Single(stats.Days);
        Assert.Equal(3000, day.InputTokens);
        Assert.Equal(500, day.OutputTokens);
        Assert.Equal(200, day.CacheCreationTokens);
        Assert.Equal(100, day.CacheReadTokens);
        Assert.Equal(2, day.MessageCount);
        Assert.Equal(3800, day.TotalTokens);
        Assert.True(day.EstimatedCost > 0m);
        Assert.Equal(3800, stats.TotalTokens);
        Assert.Equal(2, stats.TotalMessages);
    }

    /// <summary>Removes the temp directory.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
