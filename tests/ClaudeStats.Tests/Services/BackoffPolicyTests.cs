using ClaudeStats.Services;
using Xunit;

namespace ClaudeStats.Tests.Services;

/// <summary>Unit tests for <see cref="BackoffPolicy"/>.</summary>
public sealed class BackoffPolicyTests
{
    private static readonly TimeSpan Base = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Max = TimeSpan.FromMinutes(15);

    [Fact]
    public void NextDelay_GrowsExponentially_FromBase()
    {
        BackoffPolicy policy = new(Base, Max);

        Assert.Equal(TimeSpan.FromSeconds(60), policy.NextDelay());
        Assert.Equal(TimeSpan.FromSeconds(120), policy.NextDelay());
        Assert.Equal(TimeSpan.FromSeconds(240), policy.NextDelay());
        Assert.Equal(TimeSpan.FromSeconds(480), policy.NextDelay());
    }

    [Fact]
    public void NextDelay_ClampsToMax_WhenFailuresPersist()
    {
        BackoffPolicy policy = new(Base, Max);

        TimeSpan last = TimeSpan.Zero;
        for (int i = 0; i < 20; i++)
        {
            last = policy.NextDelay();
        }

        Assert.Equal(Max, last);
    }

    [Fact]
    public void Reset_ReturnsToBaseInterval()
    {
        BackoffPolicy policy = new(Base, Max);
        policy.NextDelay();
        policy.NextDelay();

        policy.Reset();

        Assert.Equal(Base, policy.NextDelay());
    }

    [Fact]
    public void NextDelay_HonorsLongerRetryAfterHint()
    {
        BackoffPolicy policy = new(Base, Max);

        TimeSpan delay = policy.NextDelay(TimeSpan.FromMinutes(30));

        Assert.Equal(TimeSpan.FromMinutes(30), delay);
    }

    [Fact]
    public void NextDelay_IgnoresShorterRetryAfterHint()
    {
        BackoffPolicy policy = new(Base, Max);
        policy.NextDelay(); // 60s
        policy.NextDelay(); // 120s

        // Computed back-off is now 240s; a shorter hint must not pull it down.
        TimeSpan delay = policy.NextDelay(TimeSpan.FromSeconds(10));

        Assert.Equal(TimeSpan.FromSeconds(240), delay);
    }

    [Fact]
    public void NextDelay_NeverBelowBase()
    {
        BackoffPolicy policy = new(Base, Max);

        TimeSpan delay = policy.NextDelay(TimeSpan.FromSeconds(1));

        Assert.Equal(Base, delay);
    }
}
