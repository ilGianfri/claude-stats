using System.Net;
using System.Net.Http;
using System.Text.Json;
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

    /// <summary>Parses the JSON body of the single request the stub captured.</summary>
    /// <param name="handler">The stub handler.</param>
    /// <returns>The parsed request body.</returns>
    private static JsonDocument RequestBody(StubHttpMessageHandler handler)
    {
        string? body = Assert.Single(handler.RequestBodies);
        Assert.NotNull(body);
        return JsonDocument.Parse(body!);
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
    public async Task RefreshAsync_SendsClaudeCodeRequestShape_WithDefaultScopes()
    {
        StubHttpMessageHandler handler = new(StubHttpMessageHandler.Json(
            """{"access_token":"new-acc","refresh_token":"new-ref","expires_in":3600}"""));
        OAuthTokenService service = CreateService(handler);

        await service.RefreshAsync(Current, CancellationToken.None);

        HttpRequestMessage request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://platform.claude.com/v1/oauth/token", request.RequestUri!.ToString());
        // The endpoint rejects the usage-style headers; the refresh must look like Claude Code's own.
        Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
        Assert.Null(request.Content.Headers.ContentType.CharSet);

        using JsonDocument body = RequestBody(handler);
        Assert.Equal("refresh_token", body.RootElement.GetProperty("grant_type").GetString());
        Assert.Equal("old-ref", body.RootElement.GetProperty("refresh_token").GetString());
        Assert.Equal("9d1c250a-e61b-44d9-88ed-5944d1962f5e", body.RootElement.GetProperty("client_id").GetString());
        Assert.Equal(
            "user:inference user:profile user:sessions:claude_code user:mcp_servers user:file_upload",
            body.RootElement.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task RefreshAsync_SendsStoredScopes_WhenCredentialsCarryThem()
    {
        StubHttpMessageHandler handler = new(StubHttpMessageHandler.Json(
            """{"access_token":"new-acc","expires_in":3600}"""));
        OAuthTokenService service = CreateService(handler);
        OAuthCredentials current = Current with { Scopes = ["user:inference", "user:profile"] };

        await service.RefreshAsync(current, CancellationToken.None);

        using JsonDocument body = RequestBody(handler);
        Assert.Equal("user:inference user:profile", body.RootElement.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task RefreshAsync_PersistsRefreshTokenExpiry_AndGrantedScopes()
    {
        StubHttpMessageHandler handler = new(StubHttpMessageHandler.Json(
            """
            {"access_token":"new-acc","refresh_token":"new-ref","expires_in":28800,
             "refresh_token_expires_in":519166,"scope":"user:inference user:profile"}
            """));
        OAuthTokenService service = CreateService(handler);

        OAuthCredentials result = await service.RefreshAsync(Current, CancellationToken.None);

        Assert.Equal(_clock.Now.AddSeconds(28800), result.ExpiresAt);
        Assert.Equal(_clock.Now.AddSeconds(519166), result.RefreshTokenExpiresAt);
        Assert.Equal(["user:inference", "user:profile"], result.Scopes);
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

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task RefreshAsync_Throws_Transient_OnServerError(HttpStatusCode status)
    {
        OAuthTokenService service = CreateService(new StubHttpMessageHandler(StubHttpMessageHandler.Status(status)));

        UsageUnavailableException ex = await Assert.ThrowsAsync<UsageUnavailableException>(
            () => service.RefreshAsync(Current, CancellationToken.None));

        Assert.True(ex.IsTransient);
    }
}
