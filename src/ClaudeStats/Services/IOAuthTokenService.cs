using ClaudeStats.Models;

namespace ClaudeStats.Services;

/// <summary>
/// Refreshes an expired OAuth access token and persists the rotated credentials.
/// </summary>
public interface IOAuthTokenService
{
    /// <summary>Exchanges the refresh token for a new access token and persists the result.</summary>
    /// <param name="current">The current (expired) credentials.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The refreshed credentials.</returns>
    Task<OAuthCredentials> RefreshAsync(OAuthCredentials current, CancellationToken ct);
}
