using System.Collections.ObjectModel;
using System.Globalization;
using ClaudeStats.Models;
using ClaudeStats.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudeStats.ViewModels;

/// <summary>
/// Drives the 30-day usage-statistics window: per-day trend bars and period totals.
/// </summary>
public sealed partial class TrendViewModel : ObservableObject
{
    private const int TrendDays = 30;
    private readonly IUsageStatsReader _reader;

    /// <summary>Whether a load is in progress.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Whether statistics data was available.</summary>
    [ObservableProperty]
    private bool _isAvailable;

    /// <summary>Total tokens across the period, formatted.</summary>
    [ObservableProperty]
    private string _totalTokensText = "—";

    /// <summary>Total messages across the period, formatted.</summary>
    [ObservableProperty]
    private string _totalMessagesText = "—";

    /// <summary>Total estimated cost across the period, formatted.</summary>
    [ObservableProperty]
    private string _totalCostText = "—";

    /// <summary>Status/subtitle text.</summary>
    [ObservableProperty]
    private string _statusText = "Loading…";

    /// <summary>Trend bars, one per day with data, ordered ascending.</summary>
    public ObservableCollection<TrendBarViewModel> Bars { get; } = [];

    /// <summary>Initializes the trend view model.</summary>
    /// <param name="reader">Usage statistics reader.</param>
    public TrendViewModel(IUsageStatsReader reader)
    {
        _reader = reader;
    }

    /// <summary>Loads and applies the latest statistics.</summary>
    /// <returns>A task that completes when loading finishes.</returns>
    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        StatusText = "Loading…";
        try
        {
            UsageStatistics statistics = await _reader.GetStatisticsAsync(TrendDays, CancellationToken.None);
            Apply(statistics);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Applies loaded statistics to the bound state.</summary>
    /// <param name="statistics">The statistics to display.</param>
    private void Apply(UsageStatistics statistics)
    {
        Bars.Clear();
        IsAvailable = statistics.IsAvailable;

        if (!statistics.IsAvailable)
        {
            TotalTokensText = "—";
            TotalMessagesText = "—";
            TotalCostText = "—";
            StatusText = "No usage statistics found. Use Claude on this machine to build history.";
            return;
        }

        long maxTokens = statistics.Days.Count > 0 ? statistics.Days.Max(d => d.TotalTokens) : 0;
        foreach (DailyUsage day in statistics.Days)
        {
            double fraction = maxTokens > 0 ? (double)day.TotalTokens / maxTokens : 0.0;
            string label = day.Date.ToString("MMM d", CultureInfo.CurrentCulture);
            string tooltip =
                $"{label}: {FormatTokens(day.TotalTokens)} tokens · {day.MessageCount:N0} msgs · {day.EstimatedCost.ToString("C2", CultureInfo.CurrentCulture)}";
            Bars.Add(new TrendBarViewModel(label, day.TotalTokens, fraction, tooltip));
        }

        TotalTokensText = FormatTokens(statistics.TotalTokens);
        TotalMessagesText = statistics.TotalMessages.ToString("N0", CultureInfo.CurrentCulture);
        TotalCostText = statistics.TotalEstimatedCost.ToString("C2", CultureInfo.CurrentCulture);
        StatusText = statistics.Days.Count == 0
            ? $"No usage in the last {TrendDays} days."
            : $"Last {TrendDays} days · estimated";
    }

    /// <summary>Formats a token count compactly (e.g. 1.2M, 345K).</summary>
    /// <param name="tokens">The token count.</param>
    /// <returns>The formatted string.</returns>
    private static string FormatTokens(long tokens)
    {
        if (tokens >= 1_000_000)
        {
            return (tokens / 1_000_000.0).ToString("0.0", CultureInfo.CurrentCulture) + "M";
        }

        if (tokens >= 1_000)
        {
            return (tokens / 1_000.0).ToString("0.0", CultureInfo.CurrentCulture) + "K";
        }

        return tokens.ToString("N0", CultureInfo.CurrentCulture);
    }
}
