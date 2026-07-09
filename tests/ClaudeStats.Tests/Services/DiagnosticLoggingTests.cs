using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using ClaudeStats.Models;
using ClaudeStats.Services;
using ClaudeStats.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace ClaudeStats.Tests.Services;

/// <summary>
/// Verifies that rejected HTTP responses are logged with enough detail to distinguish an edge/WAF
/// block from an application-layer rate limit, and that the file sink actually persists entries.
/// </summary>
public sealed class DiagnosticLoggingTests
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

    private UsageClient CreateClient(StubHttpMessageHandler handler, ILogger<UsageClient> logger)
    {
        HttpClient http = new(handler) { BaseAddress = new Uri("https://api.anthropic.com/") };
        return new UsageClient(http, _store, _token, _clock, logger);
    }

    [Fact]
    public async Task Logs_CloudflareBlock_WithHostStatusRayAndHtmlBody()
    {
        _store.ReadAsync(Arg.Any<CancellationToken>()).Returns(ValidCredentials);
        CapturingLogger<UsageClient> logger = new();
        StubHttpMessageHandler handler = new(_ =>
        {
            HttpResponseMessage response = new(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent(
                    "<!DOCTYPE html><html><head><title>Just a moment...</title></head></html>",
                    Encoding.UTF8, "text/html"),
            };
            response.Headers.Add("cf-ray", "8ab12cd34ef-FRA");
            response.Headers.Add("cf-mitigated", "challenge");
            response.Headers.Add("Server", "cloudflare");
            return response;
        });

        await Assert.ThrowsAsync<UsageUnavailableException>(
            () => CreateClient(handler, logger).GetUsageAsync(CancellationToken.None));

        string message = Assert.Single(logger.Messages);
        Assert.Contains("api.anthropic.com/api/oauth/usage", message);
        Assert.Contains("429", message);
        Assert.Contains("cf-ray=8ab12cd34ef-FRA", message);
        Assert.Contains("cf-mitigated=challenge", message);
        Assert.Contains("server=cloudflare", message);
        Assert.Contains("body[HTML]", message);
    }

    [Fact]
    public async Task Logs_AppRateLimit_WithAnthropicHeadersAndJsonBody()
    {
        _store.ReadAsync(Arg.Any<CancellationToken>()).Returns(ValidCredentials);
        CapturingLogger<UsageClient> logger = new();
        StubHttpMessageHandler handler = new(_ =>
        {
            HttpResponseMessage response = new(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent(
                    """{"type":"error","error":{"type":"rate_limit_error","message":"..."}}""",
                    Encoding.UTF8, "application/json"),
            };
            response.Headers.Add("Retry-After", "37");
            response.Headers.Add("anthropic-ratelimit-unified-5h-status", "rejected");
            response.Headers.Add("request-id", "req_0123456789");
            return response;
        });

        await Assert.ThrowsAsync<UsageUnavailableException>(
            () => CreateClient(handler, logger).GetUsageAsync(CancellationToken.None));

        string message = Assert.Single(logger.Messages);
        Assert.Contains("retry-after=37", message);
        Assert.Contains("anthropic-ratelimit-unified-5h-status=rejected", message);
        Assert.Contains("request-id=req_0123456789", message);
        Assert.Contains("body[JSON]", message);
    }

    [Fact]
    public async Task DoesNotLog_OnSuccess()
    {
        _store.ReadAsync(Arg.Any<CancellationToken>()).Returns(ValidCredentials);
        CapturingLogger<UsageClient> logger = new();
        string json = """{"five_hour":{"utilization":10.0,"resets_at":"2026-07-10T13:59:59+00:00"}}""";
        StubHttpMessageHandler handler = new(StubHttpMessageHandler.Json(json));

        await CreateClient(handler, logger).GetUsageAsync(CancellationToken.None);

        Assert.Empty(logger.Messages);
    }

    [Fact]
    public void FileLoggerProvider_WritesEntryToDailyFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), "claudestats-logtest-" + Guid.NewGuid().ToString("N"));
        try
        {
            using FileLoggerProvider provider = new(LogLevel.Information, dir);
            ILogger logger = provider.CreateLogger("ClaudeStats.Test");
            logger.LogWarning("Usage request rejected: {Diagnostic}", "GET api.anthropic.com/... -> 429");

            Assert.True(File.Exists(provider.CurrentLogFilePath));
            string contents = File.ReadAllText(provider.CurrentLogFilePath);
            Assert.Contains("[WARN ]", contents);
            Assert.Contains("ClaudeStats.Test", contents);
            Assert.Contains("Usage request rejected: GET api.anthropic.com/... -> 429", contents);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void FileLoggerProvider_RespectsMinimumLevel()
    {
        string dir = Path.Combine(Path.GetTempPath(), "claudestats-logtest-" + Guid.NewGuid().ToString("N"));
        try
        {
            using FileLoggerProvider provider = new(LogLevel.Warning, dir);
            ILogger logger = provider.CreateLogger("ClaudeStats.Test");
            logger.LogInformation("this should be filtered out");

            Assert.False(File.Exists(provider.CurrentLogFilePath));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
