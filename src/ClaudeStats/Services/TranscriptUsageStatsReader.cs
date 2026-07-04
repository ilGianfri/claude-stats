using System.IO;
using System.Text.Json;
using ClaudeStats.Json;
using ClaudeStats.Models;
using Microsoft.Extensions.Logging;

namespace ClaudeStats.Services;

/// <summary>
/// Aggregates <c>%USERPROFILE%\.claude\projects\**\*.jsonl</c> transcript files into per-day usage.
/// Streams each file line-by-line with shared read access, off the UI thread.
/// </summary>
public sealed class TranscriptUsageStatsReader : IUsageStatsReader
{
    private readonly IPricingProvider _pricing;
    private readonly IClock _clock;
    private readonly ILogger<TranscriptUsageStatsReader> _logger;
    private readonly string _projectsDirectory;

    /// <summary>Initializes the reader using the default projects directory.</summary>
    /// <param name="pricing">Pricing provider for cost estimation.</param>
    /// <param name="clock">Clock for the day window.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public TranscriptUsageStatsReader(IPricingProvider pricing, IClock clock, ILogger<TranscriptUsageStatsReader> logger)
        : this(pricing, clock, logger, DefaultProjectsDirectory())
    {
    }

    /// <summary>Initializes the reader with an explicit projects directory (used by tests).</summary>
    /// <param name="pricing">Pricing provider for cost estimation.</param>
    /// <param name="clock">Clock for the day window.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="projectsDirectory">Absolute path to the transcripts root directory.</param>
    public TranscriptUsageStatsReader(
        IPricingProvider pricing,
        IClock clock,
        ILogger<TranscriptUsageStatsReader> logger,
        string projectsDirectory)
    {
        _pricing = pricing;
        _clock = clock;
        _logger = logger;
        _projectsDirectory = projectsDirectory;
    }

    /// <summary>Computes the default transcripts root directory.</summary>
    /// <returns>The absolute path to <c>~/.claude/projects</c>.</returns>
    public static string DefaultProjectsDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude",
        "projects");

    /// <inheritdoc />
    public async Task<UsageStatistics> GetStatisticsAsync(int days, CancellationToken ct)
    {
        if (days < 1)
        {
            days = 1;
        }

        if (!Directory.Exists(_projectsDirectory))
        {
            return UsageStatistics.Unavailable;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(_projectsDirectory, "*.jsonl", SearchOption.AllDirectories);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not enumerate transcript files.");
            return UsageStatistics.Unavailable;
        }

        if (files.Length == 0)
        {
            return UsageStatistics.Unavailable;
        }

        DateOnly endDate = DateOnly.FromDateTime(_clock.Now.LocalDateTime);
        DateOnly startDate = endDate.AddDays(-(days - 1));
        Dictionary<DateOnly, DayAccumulator> byDay = [];

        foreach (string file in files)
        {
            ct.ThrowIfCancellationRequested();
            await AccumulateFileAsync(file, startDate, endDate, byDay, ct);
        }

        List<DailyUsage> daily = byDay.Values
            .Select(a => a.ToDailyUsage())
            .OrderBy(d => d.Date)
            .ToList();

        return new UsageStatistics
        {
            Days = daily,
            IsAvailable = true,
            PeriodStart = startDate,
            PeriodEnd = endDate,
        };
    }

    /// <summary>Accumulates one transcript file's assistant records into the day map.</summary>
    /// <param name="file">Transcript file path.</param>
    /// <param name="startDate">Inclusive window start.</param>
    /// <param name="endDate">Inclusive window end.</param>
    /// <param name="byDay">Accumulator map keyed by day.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the file has been processed.</returns>
    private async Task AccumulateFileAsync(
        string file,
        DateOnly startDate,
        DateOnly endDate,
        Dictionary<DateOnly, DayAccumulator> byDay,
        CancellationToken ct)
    {
        try
        {
            await using FileStream fs = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using StreamReader reader = new(fs);
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) is not null)
            {
                if (line.Length == 0)
                {
                    continue;
                }

                TranscriptRecordDto? record = TryParse(line);
                if (record is not { Type: "assistant", Timestamp: { } ts, Message.Usage: { } usage })
                {
                    continue;
                }

                DateOnly day = DateOnly.FromDateTime(ts.LocalDateTime);
                if (day < startDate || day > endDate)
                {
                    continue;
                }

                if (!byDay.TryGetValue(day, out DayAccumulator? acc))
                {
                    acc = new DayAccumulator(day);
                    byDay[day] = acc;
                }

                acc.Add(usage, _pricing.TryGet(record.Message?.Model));
            }
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Skipping unreadable transcript file {File}.", file);
        }
    }

    /// <summary>Parses a single JSON-lines record, returning null on malformed input.</summary>
    /// <param name="line">The JSON line.</param>
    /// <returns>The parsed record, or null.</returns>
    private static TranscriptRecordDto? TryParse(string line)
    {
        try
        {
            return JsonSerializer.Deserialize(line, AppJsonContext.Default.TranscriptRecordDto);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Mutable per-day accumulator.</summary>
    private sealed class DayAccumulator(DateOnly date)
    {
        private long _input;
        private long _output;
        private long _cacheWrite;
        private long _cacheRead;
        private int _messages;
        private decimal _cost;

        /// <summary>Adds one message's usage and estimated cost.</summary>
        /// <param name="usage">The message token usage.</param>
        /// <param name="price">The model price, or null when unknown.</param>
        public void Add(TranscriptUsageDto usage, ModelPrice? price)
        {
            _input += usage.InputTokens;
            _output += usage.OutputTokens;
            _cacheWrite += usage.CacheCreationInputTokens;
            _cacheRead += usage.CacheReadInputTokens;
            _messages++;

            if (price is not null)
            {
                _cost += EstimateCost(usage, price);
            }
        }

        /// <summary>Produces the immutable daily result.</summary>
        /// <returns>The aggregated <see cref="DailyUsage"/>.</returns>
        public DailyUsage ToDailyUsage() => new()
        {
            Date = date,
            InputTokens = _input,
            OutputTokens = _output,
            CacheCreationTokens = _cacheWrite,
            CacheReadTokens = _cacheRead,
            MessageCount = _messages,
            EstimatedCost = _cost,
        };

        /// <summary>Estimates the cost of one message's usage.</summary>
        /// <param name="usage">Token usage.</param>
        /// <param name="price">Model price.</param>
        /// <returns>The estimated cost in USD.</returns>
        private static decimal EstimateCost(TranscriptUsageDto usage, ModelPrice price)
        {
            const decimal million = 1_000_000m;
            return (usage.InputTokens / million * price.InputPerMTok)
                + (usage.OutputTokens / million * price.OutputPerMTok)
                + (usage.CacheCreationInputTokens / million * price.CacheWritePerMTok)
                + (usage.CacheReadInputTokens / million * price.CacheReadPerMTok);
        }
    }
}
