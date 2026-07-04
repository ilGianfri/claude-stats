using System.Net;
using System.Net.Http;
using ClaudeStats.Models;
using ClaudeStats.Services;
using ClaudeStats.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace ClaudeStats.Tests.Services;

/// <summary>Unit tests for <see cref="OAuthTokenService"/>.</summary>
public sealed class OAuthTokenServiceTests
{
    private readonly FakeClock _clock = new();
    private readonly ICredentialStore _store = Substitute.For<ICredentialStore>();

    private OAuthCredentials Current => new()
    {
        AccessToken = "old",
        RefreshToken = "old-ref",
        ExpiresAt = _clock.Now.AddSeconds(-1),
        SubscriptionType = "max",
    };

    private OAuthTokenService CreateService(StubHttpMessageHandler handler)
    {
        HttpClient http = new(handler) { BaseAddress = new Uri("https://platform.claude.com/") };
        return new OAuthTokenService(http, _store, _clock, NullLogger<OAuthTokenService>.Instance);
    }

    [Fact]
    public async Task RefreshAsync_ReturnsRotatedTokens_AndPersists()
    {
        StubHttpMessageHandler handler = new(StubHttpMessageHandler.Json(
            """{"access_token":"new-acc","refresh_token":"new-ref","expires_in":3600}"""));
        OAuthTokenService service = CreateService(handler);

        OAuthCredentials result = await service.RefreshAsync(Current, CancellationToken.None);

        Assert.Equal("new-acc", result.AccessToken);
        Assert.Equal("new-ref", result.RefreshToken);
        Assert.Equal(_clock.Now.AddSeconds(3600), result.ExpiresAt);
        await _store.Received(1).WriteAsync(
            Arg.Is<OAuthCredentials>(c => c.AccessToken == "new-acc" && c.RefreshToken == "new-ref"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_KeepsCurrentRefreshToken_WhenResponseOmitsIt()
    {
        StubHttpMessageHandler handler = new(StubHttpMessageHandler.Json(
            """{"access_token":"new-acc","expires_in":3600}"""));
        OAuthTokenService service = CreateService(handler);

        OAuthCredentials result = await service.RefreshAsync(Current, CancellationToken.None);

        Assert.Equal("old-ref", result.RefreshToken);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task RefreshAsync_Throws_HardUnavailable_OnAuthFailure(HttpStatusCode status)
    {
        OAuthTokenService service = CreateService(new StubHttpMessageHandler(StubHttpMessageHandler.Status(status)));

        UsageUnavailableException ex = await Assert.ThrowsAsync<UsageUnavailableException>(
            () => service.RefreshAsync(Current, CancellationToken.None));

        Assert.False(ex.IsTransient);
        await _store.DidNotReceive().WriteAsync(Arg.Any<OAuthCredentials>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_Throws_Transient_OnServerError()
    {
        OAuthTokenService service = CreateService(
            new StubHttpMessageHandler(StubHttpMessageHandler.Status(HttpStatusCode.InternalServerError)));

        UsageUnavailableException ex = await Assert.ThrowsAsync<UsageUnavailableException>(
            () => service.RefreshAsync(Current, CancellationToken.None));

        Assert.True(ex.IsTransient);
    }
}
