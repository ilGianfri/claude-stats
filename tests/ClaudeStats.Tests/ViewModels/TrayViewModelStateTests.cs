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

/// <summary>Icon-state and escalation-notification tests for <see cref="TrayViewModel"/> (US3).</summary>
public sealed class TrayViewModelStateTests
{
    private readonly FakeClock _clock = new();
    private readonly IUsagePoller _poller = Substitute.For<IUsagePoller>();
    private readonly IShellService _shell = Substitute.For<IShellService>();

    private TrayViewModel Create() =>
        new(_poller, _shell, _clock, new AppSettings(), new WeakReferenceMessenger());

    private void Send(TrayViewModel vm, double utilization) =>
        vm.Receive(new UsageUpdatedMessage(new UsageSnapshot
        {
            CapturedAt = _clock.Now,
            Windows =
            [
                new LimitWindow
                {
                    Kind = WindowKind.FiveHour,
                    Utilization = utilization,
                    ResetsAt = _clock.Now.AddHours(1),
                },
            ],
        }));

    [Theory]
    [InlineData(0.50, "normal.ico")]
    [InlineData(0.80, "warning.ico")]
    [InlineData(1.00, "reached.ico")]
    public void IconResourcePath_ReflectsState(double utilization, string expectedIcon)
    {
        TrayViewModel vm = Create();
        Send(vm, utilization);
        Assert.EndsWith(expectedIcon, vm.IconResourcePath);
    }

    [Fact]
    public void Transition_Normal_Warning_Reached_Then_ResetToNormal()
    {
        TrayViewModel vm = Create();

        Send(vm, 0.50);
        Assert.Equal(LimitState.Normal, vm.State);

        Send(vm, 0.85);
        Assert.Equal(LimitState.Warning, vm.State);

        Send(vm, 1.00);
        Assert.Equal(LimitState.Reached, vm.State);

        // After reset the window drops back below threshold.
        Send(vm, 0.05);
        Assert.Equal(LimitState.Normal, vm.State);
        Assert.EndsWith("normal.ico", vm.IconResourcePath);
    }

    [Fact]
    public void Notifies_OnceOnEscalation_NotOnStayingOrDropping()
    {
        TrayViewModel vm = Create();

        Send(vm, 0.50);                     // Normal — no notify
        _shell.DidNotReceive().Notify(Arg.Any<string>(), Arg.Any<string>());

        Send(vm, 0.85);                     // crosses into Warning — notify #1
        Send(vm, 0.90);                     // still Warning — no new notify
        _shell.Received(1).Notify(Arg.Any<string>(), Arg.Any<string>());

        Send(vm, 1.00);                     // escalates to Reached — notify #2
        _shell.Received(2).Notify(Arg.Any<string>(), Arg.Any<string>());
    }
}
