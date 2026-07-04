using ClaudeStats.Models;
using ClaudeStats.Services;
using Xunit;

namespace ClaudeStats.Tests.Services;

/// <summary>Unit tests for <see cref="PricingProvider"/>.</summary>
public sealed class PricingProviderTests
{
    private readonly PricingProvider _provider = new();

    [Theory]
    [InlineData("claude-opus-4-8", 15)]
    [InlineData("claude-sonnet-5", 3)]
    [InlineData("claude-haiku-4-5", 0.80)]
    public void TryGet_MatchesFamily(string modelId, double expectedInputPerMTok)
    {
        ModelPrice? price = _provider.TryGet(modelId);
        Assert.NotNull(price);
        Assert.Equal((decimal)expectedInputPerMTok, price!.InputPerMTok);
    }

    [Theory]
    [InlineData("gpt-4")]
    [InlineData("")]
    [InlineData(null)]
    public void TryGet_ReturnsNull_ForUnknownOrEmpty(string? modelId)
    {
        Assert.Null(_provider.TryGet(modelId));
    }
}
