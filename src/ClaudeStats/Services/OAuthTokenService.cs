using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using ClaudeStats.Json;
using ClaudeStats.Models;
using Microsoft.Extensions.Logging;

namespace ClaudeStats.Services;

/// <summary>
/// Refreshes the OAuth access token against the platform token endpoint and persists the rotated
/// credentials via <see cref="ICredentialStore"/>. Never logs token values.
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

        TokenRefreshRequestDto request = new()
        {
            GrantType = "refresh_token",
            RefreshToken = current.RefreshToken,
            ClientId = ClaudeApi.OAuthClientId,
        };

        HttpResponseMessage response;
        try
        {
            using JsonContent content = JsonContent.Create(request, AppJsonContext.Default.TokenRefreshRequestDto);
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

            OAuthCredentials updated = current with
            {
                AccessToken = accessToken,
                RefreshToken = body.RefreshToken is { Length: > 0 } rotated ? rotated : current.RefreshToken,
                ExpiresAt = _clock.Now.AddSeconds(body.ExpiresIn),
            };

            await _store.WriteAsync(updated, ct);
            _logger.LogInformation("Refreshed Claude OAuth access token.");
            return updated;
        }
    }
}
