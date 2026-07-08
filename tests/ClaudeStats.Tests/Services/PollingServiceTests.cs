using ClaudeStats.Configuration;
using ClaudeStats.Messages;
using ClaudeStats.Models;
using ClaudeStats.Services;
using ClaudeStats.Tests.TestSupport;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace ClaudeStats.Tests.Services;

/// <summary>Unit tests for <see cref="PollingService"/> error/back-off broadcasting.</summary>
public sealed class PollingServiceTests
{
    private readonly IUsageClient _client = Substitute.For<IUsageClient>();
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly ImmediateUiDispatcher _ui = new();
    private readonly AppSettings _settings = new();

    private PollingService Create() =>
        new(_client, _messenger, _ui, _settings, NullLogger<PollingService>.Instance);

    private static UsageSnapshot Snapshot() => new()
    {
        CapturedAt = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero),
        Windows =
        [
            new LimitWindow
            {
                Kind = WindowKind.FiveHour,
                Utilization = 0.2,
                ResetsAt = new DateTimeOffset(2026, 7, 4, 15, 0, 0, TimeSpan.Zero),
            },
        ],
    };

    [Fact]
    public async Task RefreshNowAsync_BroadcastsUpdate_OnSuccess()
    {
        _client.GetUsageAsync(Arg.Any<CancellationToken>()).Returns(Snapshot());
        UsageUpdatedMessage? received = null;
        _messenger.Register<UsageUpdatedMessage>(this, (_, m) => received = m);

        await Create().RefreshNowAsync(CancellationToken.None);

        Assert.NotNull(received);
    }

    [Fact]
    public async Task RefreshNowAsync_BroadcastsStaleWithRetryHint_On429()
    {
        _client.GetUsageAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new UsageUnavailableException("Rate limited by Claude.", isTransient: true));
        UsageErrorMessage? received = null;
        _messenger.Register<UsageErrorMessage>(this, (_, m) => received = m);

        await Create().RefreshNowAsync(CancellationToken.None);

        Assert.NotNull(received);
        Assert.Equal(LimitState.Stale, received!.State);
        Assert.Contains("Rate limited by Claude.", received.Reason);
        Assert.Contains("Retrying in", received.Reason);
    }

    [Fact]
    public async Task RefreshNowAsync_BroadcastsUnavailable_WithNoRetryHint_OnHardFailure()
    {
        _client.GetUsageAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new UsageUnavailableException("Sign in to Claude required.", isTransient: false));
        UsageErrorMessage? received = null;
        _messenger.Register<UsageErrorMessage>(this, (_, m) => received = m);

        await Create().RefreshNowAsync(CancellationToken.None);

        Assert.NotNull(received);
        Assert.Equal(LimitState.Unavailable, received!.State);
        Assert.Equal("Sign in to Claude required.", received.Reason);
        Assert.DoesNotContain("Retrying in", received.Reason);
    }

    [Fact]
    public async Task RefreshNowAsync_BroadcastsStaleWithRetryHint_OnUnexpectedError()
    {
        _client.GetUsageAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));
        UsageErrorMessage? received = null;
        _messenger.Register<UsageErrorMessage>(this, (_, m) => received = m);

        await Create().RefreshNowAsync(CancellationToken.None);

        Assert.NotNull(received);
        Assert.Equal(LimitState.Stale, received!.State);
        Assert.Contains("Retrying in", received.Reason);
    }
}
