using System.Globalization;
using ClaudeStats.Models;
using ClaudeStats.Services;
using ClaudeStats.ViewModels;
using NSubstitute;
using Xunit;

namespace ClaudeStats.Tests.ViewModels;

/// <summary>Unit tests for <see cref="TrendViewModel"/>.</summary>
public sealed class TrendViewModelTests
{
    private readonly IUsageStatsReader _reader = Substitute.For<IUsageStatsReader>();

    private static DailyUsage Day(int day, long input) => new()
    {
        Date = new DateOnly(2026, 7, day),
        InputTokens = input,
        MessageCount = 1,
        EstimatedCost = 0.5m,
    };

    [Fact]
    public async Task LoadAsync_PopulatesBars_AndTotals_WhenAvailable()
    {
        UsageStatistics stats = new()
        {
            IsAvailable = true,
            Days = [Day(1, 1000), Day(2, 2000)],
        };
        _reader.GetStatisticsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(stats);
        TrendViewModel vm = new(_reader);

        await vm.LoadAsync();

        Assert.True(vm.IsAvailable);
        Assert.Equal(2, vm.Bars.Count);
        Assert.Equal(0.5, vm.Bars[0].Fraction, 3); // 1000 / 2000
        Assert.Equal(1.0, vm.Bars[1].Fraction, 3);
        string expectedTokens = (3000 / 1000.0).ToString("0.0", CultureInfo.CurrentCulture) + "K";
        Assert.Equal(expectedTokens, vm.TotalTokensText);
        Assert.Equal(2.ToString("N0", CultureInfo.CurrentCulture), vm.TotalMessagesText);
    }

    [Fact]
    public async Task LoadAsync_ShowsUnavailable_WhenNoData()
    {
        _reader.GetStatisticsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(UsageStatistics.Unavailable);
        TrendViewModel vm = new(_reader);

        await vm.LoadAsync();

        Assert.False(vm.IsAvailable);
        Assert.Empty(vm.Bars);
        Assert.Contains("No usage statistics", vm.StatusText);
    }
}
