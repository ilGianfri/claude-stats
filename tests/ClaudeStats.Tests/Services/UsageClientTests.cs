using System.Globalization;
using System.Net;
using System.Net.Http;
using ClaudeStats.Models;
using ClaudeStats.Services;
using ClaudeStats.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace ClaudeStats.Tests.Services;

/// <summary>Unit tests for <see cref="UsageClient"/>.</summary>
public sealed class UsageClientTests
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
    public async Task GetUsageAsync_MapsFlatWindows_AndPicksMostConstraining()
    {
        _store.ReadAsync(Arg.Any<CancellationToken>()).Returns(ValidCredentials);
        DateTimeOffset fiveHourReset = _clock.Now.AddHours(3);
        DateTimeOffset weeklyReset = _clock.Now.AddDays(5);
        // utilization is a percentage (0-100); resets_at is an ISO 8601 timestamp.
        string json = """
            {"five_hour":{"utilization":42.0,"resets_at":"FIVE"},
             "seven_day":{"utilization":80.0,"resets_at":"WEEK"}}
            """
            .Replace("FIVE", Iso(fiveHourReset))
            .Replace("WEEK", Iso(weeklyReset));
        UsageClient client = CreateClient(new StubHttpMessageHandler(StubHttpMessageHandler.Json(json)));

        UsageSnapshot snapshot = await client.GetUsageAsync(CancellationToken.None);

        Assert.Equal(2, snapshot.Windows.Count);
        Assert.NotNull(snapshot.MostConstraining);
        Assert.Equal(WindowKind.Weekly, snapshot.MostConstraining!.Kind);
        Assert.Equal(80.0, snapshot.MostConstraining.Percent, 3);
        Assert.Equal(42.0, snapshot.Windows.Single(w => w.Kind == WindowKind.FiveHour).Percent, 3);
        Assert.Equal(fiveHourReset, snapshot.Windows.Single(w => w.Kind == WindowKind.FiveHour).ResetsAt);
    }

    [Fact]
    public async Task GetUsageAsync_ParsesLimitsArrayVariant_WhenFlatKeysAbsent()
    {
        _store.ReadAsync(Arg.Any<CancellationToken>()).Returns(ValidCredentials);
        string reset = Iso(_clock.Now.AddHours(2));
        string json = """
            {"limits":[{"percent":30,"resets_at":"R"},
                       {"percent":55,"resets_at":"R"}]}
            """
            .Replace("R", reset);
        UsageClient client = CreateClient(new StubHttpMessageHandler(StubHttpMessageHandler.Json(json)));

        UsageSnapshot snapshot = await client.GetUsageAsync(CancellationToken.None);

        Assert.Equal(2, snapshot.Windows.Count);
        Assert.Equal(55.0, snapshot.MostConstraining!.Percent, 3);
    }

    [Fact]
    public async Task GetUsageAsync_LabelsScopedLimitsDynamically()
    {
        _store.ReadAsync(Arg.Any<CancellationToken>()).Returns(ValidCredentials);
        string reset = Iso(_clock.Now.AddDays(3));
        string json = """
            {"limits":[
              {"kind":"session","percent":40,"resets_at":"R","scope":null},
              {"kind":"weekly_all","percent":20,"resets_at":"R","scope":null},
              {"kind":"weekly_scoped","percent":28,"resets_at":"R","scope":{"model":{"display_name":"Fable"}}}
            ]}
            """
            .Replace("R", reset);
        UsageClient client = CreateClient(new StubHttpMessageHandler(StubHttpMessageHandler.Json(json)));

        UsageSnapshot snapshot = await client.GetUsageAsync(CancellationToken.None);

        Assert.Collection(snapshot.Windows,
            w => Assert.Equal("5-hour", w.DisplayName),
            w => Assert.Equal("Weekly", w.DisplayName),
            w => Assert.Equal("Weekly (Fable)", w.DisplayName));
    }

    [Fact]
    public async Task GetUsageAsync_SkipsWindows_WithNullResetOrUtilization()
    {
        _store.ReadAsync(Arg.Any<CancellationToken>()).Returns(ValidCredentials);
        string json = """
            {"five_hour":{"utilization":40.0,"resets_at":"R"},
             "seven_day_opus":null,
             "seven_day_sonnet":{"utilization":null,"resets_at":null}}
            """
            .Replace("R", Iso(_clock.Now.AddHours(1)));
        UsageClient client = CreateClient(new StubHttpMessageHandler(StubHttpMessageHandler.Json(json)));

        UsageSnapshot snapshot = await client.GetUsageAsync(CancellationToken.None);

        Assert.Single(snapshot.Windows);
        Assert.Equal(WindowKind.FiveHour, snapshot.Windows[0].Kind);
    }

    [Fact]
    public async Task GetUsageAsync_ParsesRealWorldPayload()
    {
        // Captured verbatim from GET /api/oauth/usage (percentages only; not secret).
        _store.ReadAsync(Arg.Any<CancellationToken>()).Returns(ValidCredentials);
        const string json = """
            {"five_hour":{"utilization":40.0,"resets_at":"2026-07-05T02:19:59.734086+00:00","limit_dollars":null,"used_dollars":null,"remaining_dollars":null},
             "seven_day":{"utilization":20.0,"resets_at":"2026-07-10T13:59:59.734105+00:00","limit_dollars":null,"used_dollars":null,"remaining_dollars":null},
             "seven_day_oauth_apps":null,"seven_day_opus":null,"seven_day_sonnet":null,"cinder_cove":null,
             "extra_usage":{"is_enabled":false,"monthly_limit":null,"utilization":null},
             "limits":[{"kind":"session","group":"session","percent":40,"severity":"normal","resets_at":"2026-07-05T02:19:59.734086+00:00","scope":null,"is_active":true},
                       {"kind":"weekly_all","group":"weekly","percent":20,"severity":"normal","resets_at":"2026-07-10T13:59:59.734105+00:00","scope":null,"is_active":false},
                       {"kind":"weekly_scoped","group":"weekly","percent":28,"severity":"normal","resets_at":"2026-07-10T13:59:59.734378+00:00","scope":{"model":{"id":null,"display_name":"Fable"}},"is_active":false}],
             "spend":{"used":{"amount_minor":0,"currency":"USD","exponent":2},"percent":0,"enabled":false}}
            """;
        UsageClient client = CreateClient(new StubHttpMessageHandler(StubHttpMessageHandler.Json(json)));

        UsageSnapshot snapshot = await client.GetUsageAsync(CancellationToken.None);

        // limits[] is preferred: session + weekly_all + weekly_scoped (Fable) = 3 windows.
        Assert.Equal(3, snapshot.Windows.Count);
        Assert.Equal(40.0, snapshot.Windows.Single(w => w.DisplayName == "5-hour").Percent, 3);
        Assert.Equal(20.0, snapshot.Windows.Single(w => w.DisplayName == "Weekly").Percent, 3);
        LimitWindow scoped = snapshot.Windows.Single(w => w.DisplayName == "Weekly (Fable)");
        Assert.Equal(28.0, scoped.Percent, 3);
        Assert.Equal("5-hour", snapshot.MostConstraining!.DisplayName);
    }

    [Fact]
    public async Task GetUsageAsync_RefreshesOnce_AndRetries_On401()
    {
        _store.ReadAsync(Arg.Any<CancellationToken>()).Returns(ValidCredentials);
        OAuthCredentials refreshed = ValidCredentials with { AccessToken = "tok-2" };
        _token.RefreshAsync(Arg.Any<OAuthCredentials>(), Arg.Any<CancellationToken>()).Returns(refreshed);

        string json = """{"five_hour":{"utilization":10.0,"resets_at":"R"}}"""
            .Replace("R", Iso(_clock.Now.AddHours(1)));
        StubHttpMessageHandler handler = new(
            StubHttpMessageHandler.Status(HttpStatusCode.Unauthorized),
            StubHttpMessageHandler.Json(json));
        UsageClient client = CreateClient(handler);

        UsageSnapshot snapshot = await client.GetUsageAsync(CancellationToken.None);

        await _token.Received(1).RefreshAsync(Arg.Any<OAuthCredentials>(), Arg.Any<CancellationToken>());
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("tok-2", handler.Requests[1].Headers.Authorization!.Parameter);
        Assert.Single(snapshot.Windows);
    }

    [Fact]
    public async Task GetUsageAsync_Throws_HardUnavailable_WhenNoCredentials()
    {
        _store.ReadAsync(Arg.Any<CancellationToken>()).Returns((OAuthCredentials?)null);
        UsageClient client = CreateClient(new StubHttpMessageHandler(StubHttpMessageHandler.Status(HttpStatusCode.OK)));

        UsageUnavailableException ex = await Assert.ThrowsAsync<UsageUnavailableException>(
            () => client.GetUsageAsync(CancellationToken.None));

        Assert.False(ex.IsTransient);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetUsageAsync_Throws_Transient_OnRateLimitOrServerError(HttpStatusCode status)
    {
        _store.ReadAsync(Arg.Any<CancellationToken>()).Returns(ValidCredentials);
        UsageClient client = CreateClient(new StubHttpMessageHandler(StubHttpMessageHandler.Status(status)));

        UsageUnavailableException ex = await Assert.ThrowsAsync<UsageUnavailableException>(
            () => client.GetUsageAsync(CancellationToken.None));

        Assert.True(ex.IsTransient);
    }

    [Fact]
    public async Task GetUsageAsync_Throws_Transient_WhenNoWindowsParsed()
    {
        _store.ReadAsync(Arg.Any<CancellationToken>()).Returns(ValidCredentials);
        UsageClient client = CreateClient(new StubHttpMessageHandler(StubHttpMessageHandler.Json("{}")));

        UsageUnavailableException ex = await Assert.ThrowsAsync<UsageUnavailableException>(
            () => client.GetUsageAsync(CancellationToken.None));

        Assert.True(ex.IsTransient);
    }

    [Fact]
    public async Task GetUsageAsync_RefreshesProactively_WhenTokenExpired()
    {
        OAuthCredentials expired = ValidCredentials with { ExpiresAt = _clock.Now.AddSeconds(-1) };
        _store.ReadAsync(Arg.Any<CancellationToken>()).Returns(expired);
        _token.RefreshAsync(Arg.Any<OAuthCredentials>(), Arg.Any<CancellationToken>())
            .Returns(expired with { AccessToken = "tok-fresh", ExpiresAt = _clock.Now.AddHours(1) });

        string json = """{"five_hour":{"utilization":20.0,"resets_at":"R"}}"""
            .Replace("R", Iso(_clock.Now.AddHours(1)));
        UsageClient client = CreateClient(new StubHttpMessageHandler(StubHttpMessageHandler.Json(json)));

        await client.GetUsageAsync(CancellationToken.None);

        await _token.Received(1).RefreshAsync(Arg.Any<OAuthCredentials>(), Arg.Any<CancellationToken>());
    }
}
