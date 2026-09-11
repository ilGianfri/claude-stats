using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClaudeStats.Json;
using ClaudeStats.Models;
using Microsoft.Extensions.Logging;

namespace ClaudeStats.Services;

/// <summary>
/// Refreshes the OAuth access token against the platform token endpoint and persists the rotated
/// credentials via <see cref="ICredentialStore"/>. The request mirrors the one Claude Code itself
/// sends (JSON body including <c>scope</c>, plain <c>application/json</c> content type): the
/// endpoint answers <c>429 rate_limit_error</c> to any other shape. Never logs token values.
/// </summary>
public sealed class OAuthTokenService : IOAuthTokenService
{
    private readonly HttpClient _http;
    private readonly ICredentialStore _store;
    private readonly IClock _clock;
    private readonly ILogger<OAuthTokenService> _logger;

    /// <summary>Initializes the token service.</summary>
    /// <param name="http">Typed HTTP client configured for the platform host.</param>
    /// <param name="store">Credential store used to persist refreshed tokens.</param>
    /// <param name="clock">Clock used to compute the new expiry.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public OAuthTokenService(HttpClient http, ICredentialStore store, IClock clock, ILogger<OAuthTokenService> logger)
    {
        _http = http;
        _store = store;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OAuthCredentials> RefreshAsync(OAuthCredentials current, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(current);

        IReadOnlyList<string> scopes = current.Scopes is { Count: > 0 } stored ? stored : ClaudeApi.DefaultOAuthScopes;
        TokenRefreshRequestDto request = new()
        {
            GrantType = "refresh_token",
            RefreshToken = current.RefreshToken,
            ClientId = ClaudeApi.OAuthClientId,
            Scope = string.Join(' ', scopes),
        };

        HttpResponseMessage response;
        try
        {
            // Plain "application/json" (no charset parameter), exactly like Claude Code's own client.
            string json = JsonSerializer.Serialize(request, AppJsonContext.Default.TokenRefreshRequestDto);
            using StringContent content = new(json, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            response = await _http.PostAsync(ClaudeApi.TokenPath, content, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new UsageUnavailableException("Could not reach Claude to refresh sign-in.", isTransient: true, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new UsageUnavailableException("Timed out refreshing Claude sign-in.", isTransient: true, ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Token refresh rejected: {Diagnostic}",
                    await HttpDiagnostics.DescribeAsync(response, ct));
            }

            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new UsageUnavailableException("Sign in to Claude required.", isTransient: false);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new UsageUnavailableException(
                    $"Token refresh failed ({(int)response.StatusCode}).", isTransient: true);
            }

            TokenRefreshResponseDto? body = await response.Content.ReadFromJsonAsync(
                AppJsonContext.Default.TokenRefreshResponseDto, ct);

            if (body?.AccessToken is not { Length: > 0 } accessToken)
            {
                throw new UsageUnavailableException("Token refresh returned no access token.", isTransient: true);
            }

            DateTimeOffset now = _clock.Now;
            OAuthCredentials updated = current with
            {
                AccessToken = accessToken,
                RefreshToken = body.RefreshToken is { Length: > 0 } rotated ? rotated : current.RefreshToken,
                ExpiresAt = now.AddSeconds(body.ExpiresIn),
                RefreshTokenExpiresAt = body.RefreshTokenExpiresIn is { } refreshLifetime
                    ? now.AddSeconds(refreshLifetime)
                    : current.RefreshTokenExpiresAt,
                Scopes = ParseScopes(body.Scope) ?? scopes,
            };

            await _store.WriteAsync(updated, ct);
            _logger.LogInformation("Refreshed Claude OAuth access token.");
            return updated;
        }
    }

    /// <summary>Splits a space-separated scope string into a list.</summary>
    /// <param name="scope">The raw scope string from the token response.</param>
    /// <returns>The scopes, or null when the string is empty.</returns>
    private static IReadOnlyList<string>? ParseScopes(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        string[] parts = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 ? parts : null;
    }
}
