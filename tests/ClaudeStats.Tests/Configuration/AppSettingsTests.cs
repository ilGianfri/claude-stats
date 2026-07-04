using ClaudeStats.Configuration;
using Xunit;

namespace ClaudeStats.Tests.Configuration;

/// <summary>Unit tests for <see cref="AppSettings"/>.</summary>
public sealed class AppSettingsTests
{
    [Theory]
    [InlineData(60, 60)]
    [InlineData(120, 120)]
    [InlineData(10, 60)]   // floored to respect the endpoint rate limit
    [InlineData(0, 60)]
    public void RefreshInterval_IsFlooredAt60Seconds(int configured, int expectedSeconds)
    {
        AppSettings settings = new() { RefreshIntervalSeconds = configured };
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), settings.RefreshInterval);
    }

    [Fact]
    public void Defaults_AreSensible()
    {
        AppSettings settings = new();
        Assert.Equal(60, settings.RefreshIntervalSeconds);
        Assert.False(settings.LaunchAtStartup);
        Assert.Equal(80, settings.WarningThresholdPercent);
    }
}
