using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudeStats.Json;
using ClaudeStats.Models;
using Microsoft.Extensions.Logging;

namespace ClaudeStats.Services;

/// <summary>
/// Reads the current usage snapshot from <c>GET /api/oauth/usage</c>, reusing the local Claude OAuth
/// token and refreshing it on a 401. Parses defensively (flat-map and <c>limits[]</c> shapes) and
/// isolates all knowledge of the unofficial wire contract.
/// </summary>
public sealed class UsageClient : IUsageClient
{
    private readonly HttpClient _http;
    private readonly ICredentialStore _store;
    private readonly IOAuthTokenService _token;
    private readonly IClock _clock;
    private readonly ILogger<UsageClient> _logger;

    /// <summary>Initializes the usage client.</summary>
    /// <param name="http">Typed HTTP client configured for the Anthropic API host.</param>
    /// <param name="store">Credential store for the OAuth token.</param>
    /// <param name="token">Token service used to refresh on expiry/401.</param>
    /// <param name="clock">Clock for capture timestamps and expiry checks.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public UsageClient(
        HttpClient http,
        ICredentialStore store,
        IOAuthTokenService token,
        IClock clock,
        ILogger<UsageClient> logger)
    {
        _http = http;
        _store = store;
        _token = token;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UsageSnapshot> GetUsageAsync(CancellationToken ct)
    {
        OAuthCredentials? credentials = await _store.ReadAsync(ct);
        if (credentials is null)
        {
            throw new UsageUnavailableException("Sign in to Claude required.", isTransient: false);
        }

        if (credentials.IsExpired(_clock.Now))
        {
            credentials = await _token.RefreshAsync(credentials, ct);
        }

        HttpResponseMessage response = await SendAsync(credentials.AccessToken, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            credentials = await _token.RefreshAsync(credentials, ct);
            response = await SendAsync(credentials.AccessToken, ct);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Usage request rejected: {Diagnostic}",
                    await HttpDiagnostics.DescribeAsync(response, ct));
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new UsageUnavailableException("Sign in to Claude required.", isTransient: false);
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new UsageUnavailableException(
                    "Rate limited by Claude.", isTransient: true, retryAfter: GetRetryAfter(response));
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new UsageUnavailableException(
                    $"Usage endpoint error ({(int)response.StatusCode}).", isTransient: true);
            }

            UsageResponseDto? dto;
            try
            {
                dto = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.UsageResponseDto, ct);
            }
            catch (JsonException ex)
            {
                throw new UsageUnavailableException("Unexpected usage response format.", isTransient: true, ex);
            }

            IReadOnlyList<LimitWindow> windows = MapWindows(dto);
            if (windows.Count == 0)
            {
                throw new UsageUnavailableException("No usage data returned.", isTransient: true);
            }

            return new UsageSnapshot
            {
                Windows = windows,
                CapturedAt = _clock.Now,
                Source = UsageSource.Endpoint,
            };
        }
    }

    /// <summary>Issues the authenticated GET request, translating transport failures.</summary>
    /// <param name="accessToken">Bearer token to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The HTTP response.</returns>
    private async Task<HttpResponseMessage> SendAsync(string accessToken, CancellationToken ct)
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, ClaudeApi.UsagePath);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return await _http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new UsageUnavailableException("Could not reach Claude.", isTransient: true, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new UsageUnavailableException("Timed out contacting Claude.", isTransient: true, ex);
        }
    }

    /// <summary>Reads the Retry-After hint from a response, converting an absolute date to a delay.</summary>
    /// <param name="response">The HTTP response to inspect.</param>
    /// <returns>The requested wait, or null when the header is absent or already elapsed.</returns>
    private TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        RetryConditionHeaderValue? header = response.Headers.RetryAfter;
        if (header is null)
        {
            return null;
        }

        TimeSpan? delta = header.Delta ?? (header.Date is { } date ? date - _clock.Now : null);
        return delta is { } value && value > TimeSpan.Zero ? value : null;
    }

    /// <summary>
    /// Maps the response DTO to domain windows. Prefers the richer <c>limits[]</c> array (which
    /// includes dynamic, per-model scoped limits), falling back to the flat-map keys.
    /// </summary>
    /// <param name="dto">The deserialized response, or null.</param>
    /// <returns>The parsed windows (possibly empty).</returns>
    private static IReadOnlyList<LimitWindow> MapWindows(UsageResponseDto? dto)
    {
        if (dto is null)
        {
            return [];
        }

        List<LimitWindow> windows = [];

        if (dto.Limits is { Count: > 0 } limits)
        {
            foreach (UsageLimitDto limit in limits)
            {
                double? percent = limit.Percent ?? limit.Utilization;
                if (percent is null || limit.ResetsAt is null)
                {
                    continue;
                }

                (WindowKind kind, string label) = DescribeLimit(limit);
                windows.Add(new LimitWindow
                {
                    Kind = kind,
                    Label = label,
                    Utilization = percent.Value / 100.0,
                    ResetsAt = limit.ResetsAt.Value.ToLocalTime(),
                });
            }

            if (windows.Count > 0)
            {
                return windows;
            }
        }

        // Fallback: flat-map keys.
        AddWindow(windows, WindowKind.FiveHour, dto.FiveHour);
        AddWindow(windows, WindowKind.Weekly, dto.SevenDay);
        AddWindow(windows, WindowKind.WeeklyOpus, dto.SevenDayOpus);
        return windows;
    }

    /// <summary>Derives a window kind and dynamic display label from a limits[] entry.</summary>
    /// <param name="limit">The limit entry.</param>
    /// <returns>The mapped kind and a human-readable label.</returns>
    private static (WindowKind Kind, string Label) DescribeLimit(UsageLimitDto limit)
    {
        string? scopeName = limit.Scope?.Model?.DisplayName;
        return limit.Kind switch
        {
            "session" => (WindowKind.FiveHour, "5-hour"),
            "weekly_all" => (WindowKind.Weekly, "Weekly"),
            "weekly_opus" => (WindowKind.WeeklyOpus, "Weekly (Opus)"),
            "weekly_scoped" => (WindowKind.Unknown,
                scopeName is { Length: > 0 } ? $"Weekly ({scopeName})" : "Weekly (scoped)"),
            _ => (WindowKind.Unknown, scopeName is { Length: > 0 } ? scopeName : Prettify(limit.Kind)),
        };
    }

    /// <summary>Turns a snake_case kind into a title-cased label (e.g. "weekly_all" → "Weekly All").</summary>
    /// <param name="kind">The raw kind string.</param>
    /// <returns>A human-readable label.</returns>
    private static string Prettify(string? kind)
    {
        if (string.IsNullOrEmpty(kind))
        {
            return "Other";
        }

        IEnumerable<string> words = kind.Split('_')
            .Where(part => part.Length > 0)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]);
        return string.Join(' ', words);
    }

    /// <summary>Adds a mapped window to the list when the DTO has usable values.</summary>
    /// <param name="windows">Target list.</param>
    /// <param name="kind">Window kind.</param>
    /// <param name="dto">Source window DTO, or null.</param>
    private static void AddWindow(List<LimitWindow> windows, WindowKind kind, UsageWindowDto? dto)
    {
        // The endpoint reports utilization as a percentage (0–100); the domain uses a 0–1 fraction.
        if (dto?.Utilization is not { } percent || dto.ResetsAt is not { } resetsAt)
        {
            return;
        }

        windows.Add(new LimitWindow
        {
            Kind = kind,
            Utilization = percent / 100.0,
            ResetsAt = resetsAt.ToLocalTime(),
        });
    }
}
