using System.Globalization;
using System.Net;
using System.Net.Http;
using ClaudeStats.Models;
using ClaudeStats.Services;
using ClaudeStats.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace ClaudeStats.Tests.Services;

/// <summary>
/// Tests the hybrid token-refresh policy: defer to the token Claude Code writes into the shared
/// credentials file, and cool down hard after a failed refresh so the rate-limited token endpoint
/// is never stormed.
/// </summary>
public sealed class UsageClientRefreshTests
{
    private readonly FakeClock _clock = new();
    private readonly ICredentialStore _store = Substitute.For<ICredentialStore>();
    private readonly IOAuthTokenService _token = Substitute.For<IOAuthTokenService>();

    private OAuthCredentials ValidCredentials => new()
    {
        AccessToken = "tok-1",
        RefreshToken = "refresh-1",
        ExpiresAt = _clock.Now.AddHours(1),
    };

    private static string Iso(DateTimeOffset value) => value.ToString("o", CultureInfo.InvariantCulture);

    private UsageClient CreateClient(StubHttpMessageHandler handler)
    {
        HttpClient http = new(handler) { BaseAddress = new Uri("https://api.anthropic.com/") };
        return new UsageClient(http, _store, _token, _clock, NullLogger<UsageClient>.Instance);
    }

    [Fact]
    public async Task ExpiredToken_FailedRefresh_CoolsDown_AndDoesNotRetryImmediately()
    {
        OAuthCredentials expired = ValidCredentials with { ExpiresAt = _clock.Now.AddSeconds(-1) };
        _store.ReadAsync(Arg.Any<CancellationToken>()).Returns(expired);
        _token.RefreshAsync(Arg.Any<OAuthCredentials>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UsageUnavailableException("Rate limited by Claude.", isTransient: true));
        UsageClient client = CreateClient(new StubHttpMessageHandler(StubHttpMessageHandler.Json("{}")));

        // First poll: attempts the refresh, it fails, a cooldown is armed.
        await Assert.ThrowsAsync<UsageUnavailableException>(() => client.GetUsageAsync(CancellationToken.None));
        // Second poll a moment later: must NOT hit the token endpoint again.
        await Assert.ThrowsAsync<UsageUnavailableException>(() => client.GetUsageAsync(CancellationToken.None));

        await _token.Received(1).RefreshAsync(Arg.Any<OAuthCredentials>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExpiredToken_RetriesRefresh_OnceCooldownElapses()
    {
        OAuthCredentials expired = ValidCredentials with { ExpiresAt = _clock.Now.AddSeconds(-1) };
        _store.ReadAsync(Arg.Any<CancellationToken>()).Returns(expired);
        _token.RefreshAsync(Arg.Any<OAuthCredentials>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UsageUnavailableException("Rate limited by Claude.", isTransient: true));
        UsageClient client = CreateClient(new StubHttpMessageHandler(StubHttpMessageHandler.Json("{}")));

        await Assert.ThrowsAsync<UsageUnavailableException>(() => client.GetUsageAsync(CancellationToken.None));
        _clock.Now = _clock.Now.AddHours(1); // well past the first (~15 min) cooldown
        await Assert.ThrowsAsync<UsageUnavailableException>(() => client.GetUsageAsync(CancellationToken.None));

        await _token.Received(2).RefreshAsync(Arg.Any<OAuthCredentials>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExpiredToken_DefersToClaudeCode_WhenFileHasFreshToken_NoNetworkRefresh()
    {
        OAuthCredentials stale = ValidCredentials with { AccessToken = "tok-old", ExpiresAt = _clock.Now.AddSeconds(-1) };
        OAuthCredentials fresh = ValidCredentials with { AccessToken = "tok-new", ExpiresAt = _clock.Now.AddHours(1) };
        // Top-of-method read sees the stale token; the re-read inside the refresh path sees the token
        // Claude Code has since written.
        _store.ReadAsync(Arg.Any<CancellationToken>()).Returns(stale, fresh);
        string json = """{"five_hour":{"utilization":12.0,"resets_at":"R"}}""".Replace("R", Iso(_clock.Now.AddHours(1)));
        StubHttpMessageHandler handler = new(StubHttpMessageHandler.Json(json));
        UsageClient client = CreateClient(handler);

        UsageSnapshot snapshot = await client.GetUsageAsync(CancellationToken.None);

        await _token.DidNotReceive().RefreshAsync(Arg.Any<OAuthCredentials>(), Arg.Any<CancellationToken>());
        Assert.Equal("tok-new", handler.Requests[0].Headers.Authorization!.Parameter);
        Assert.Single(snapshot.Windows);
    }

    [Fact]
    public async Task Usage401_DefersToClaudeCode_WhenFileTokenChanged_NoRefresh()
    {
        OAuthCredentials current = ValidCredentials with { AccessToken = "tok-1" };
        OAuthCredentials rotated = ValidCredentials with { AccessToken = "tok-2", ExpiresAt = _clock.Now.AddHours(1) };
        _store.ReadAsync(Arg.Any<CancellationToken>()).Returns(current, rotated);
        string json = """{"five_hour":{"utilization":5.0,"resets_at":"R"}}""".Replace("R", Iso(_clock.Now.AddHours(1)));
        StubHttpMessageHandler handler = new(
            StubHttpMessageHandler.Status(HttpStatusCode.Unauthorized),
            StubHttpMessageHandler.Json(json));
        UsageClient client = CreateClient(handler);

        UsageSnapshot snapshot = await client.GetUsageAsync(CancellationToken.None);

        await _token.DidNotReceive().RefreshAsync(Arg.Any<OAuthCredentials>(), Arg.Any<CancellationToken>());
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("tok-1", handler.Requests[0].Headers.Authorization!.Parameter);
        Assert.Equal("tok-2", handler.Requests[1].Headers.Authorization!.Parameter);
        Assert.Single(snapshot.Windows);
    }
}
