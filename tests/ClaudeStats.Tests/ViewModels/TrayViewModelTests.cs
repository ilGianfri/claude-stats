using ClaudeStats.Configuration;
using ClaudeStats.Messages;
using ClaudeStats.Models;
using ClaudeStats.Services;
using ClaudeStats.Tests.TestSupport;
using ClaudeStats.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using NSubstitute;
using Xunit;

namespace ClaudeStats.Tests.ViewModels;

/// <summary>Unit tests for <see cref="TrayViewModel"/>.</summary>
public sealed class TrayViewModelTests
{
    private readonly FakeClock _clock = new();
    private readonly IUsagePoller _poller = Substitute.For<IUsagePoller>();
    private readonly IShellService _shell = Substitute.For<IShellService>();
    private readonly IUpdateChecker _updater = Substitute.For<IUpdateChecker>();

    private TrayViewModel Create() =>
        new(_poller, _shell, _clock, new AppSettings(), new WeakReferenceMessenger(), _updater);

    private LimitWindow Window(WindowKind kind, double utilization, TimeSpan resetIn) => new()
    {
        Kind = kind,
        Utilization = utilization,
        ResetsAt = _clock.Now + resetIn,
    };

    [Fact]
    public void Receive_Update_PicksMostConstraining_AndFormatsHeadline()
    {
        TrayViewModel vm = Create();
        UsageSnapshot snapshot = new()
        {
            CapturedAt = _clock.Now,
            Windows =
            [
                Window(WindowKind.FiveHour, 0.42, TimeSpan.FromHours(3) + TimeSpan.FromMinutes(13)),
                Window(WindowKind.Weekly, 0.10, TimeSpan.FromDays(5)),
            ],
        };

        vm.Receive(new UsageUpdatedMessage(snapshot));

        Assert.Equal("42%", vm.HeadlineText);
        Assert.Equal(LimitState.Normal, vm.State);
        Assert.Equal(2, vm.Windows.Count);
        Assert.Equal("resets in 3h 13m", vm.ResetText);
    }

    [Theory]
    [InlineData(0.85, LimitState.Warning)]
    [InlineData(1.00, LimitState.Reached)]
    [InlineData(0.50, LimitState.Normal)]
    public void Receive_Update_DerivesState(double utilization, LimitState expected)
    {
        TrayViewModel vm = Create();
        UsageSnapshot snapshot = new()
        {
            CapturedAt = _clock.Now,
            Windows = [Window(WindowKind.FiveHour, utilization, TimeSpan.FromHours(1))],
        };

        vm.Receive(new UsageUpdatedMessage(snapshot));

        Assert.Equal(expected, vm.State);
    }

    [Fact]
    public void Receive_Error_Unavailable_ClearsDisplay()
    {
        TrayViewModel vm = Create();

        vm.Receive(new UsageErrorMessage(LimitState.Unavailable, "Sign in to Claude required."));

        Assert.Equal(LimitState.Unavailable, vm.State);
        Assert.Equal("—", vm.HeadlineText);
        Assert.Empty(vm.Windows);
        Assert.Equal("Sign in to Claude required.", vm.StatusText);
    }

    [Fact]
    public void Receive_Error_Stale_KeepsLastKnownValues()
    {
        TrayViewModel vm = Create();
        vm.Receive(new UsageUpdatedMessage(new UsageSnapshot
        {
            CapturedAt = _clock.Now,
            Windows = [Window(WindowKind.FiveHour, 0.42, TimeSpan.FromHours(2))],
        }));

        vm.Receive(new UsageErrorMessage(LimitState.Stale, "Rate limited by Claude; will retry."));

        Assert.Equal(LimitState.Stale, vm.State);
        Assert.Equal("42%", vm.HeadlineText);
        Assert.Single(vm.Windows);
        Assert.Contains("showing last known", vm.StatusText);
    }

    [Fact]
    public async Task RefreshCommand_InvokesPoller()
    {
        TrayViewModel vm = Create();
        await vm.RefreshCommand.ExecuteAsync(null);
        await _poller.Received(1).RefreshNowAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ExitCommand_InvokesShell()
    {
        TrayViewModel vm = Create();
        vm.ExitCommand.Execute(null);
        _shell.Received(1).Exit();
    }
}
