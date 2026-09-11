namespace ClaudeStats.Models;

/// <summary>
/// OAuth credentials parsed from the Claude credentials file. Token values are secret and are
/// never logged.
/// </summary>
public sealed record OAuthCredentials
{
    /// <summary>Default clock skew applied when deciding whether the access token is expired.</summary>
    public static readonly TimeSpan ExpirySkew = TimeSpan.FromSeconds(60);

    /// <summary>Bearer token used to authenticate against the usage endpoint.</summary>
    public required string AccessToken { get; init; }

    /// <summary>Refresh token (rotated on each refresh).</summary>
    public required string RefreshToken { get; init; }

    /// <summary>Instant at which the access token expires.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Instant at which the refresh token itself expires, when known.</summary>
    public DateTimeOffset? RefreshTokenExpiresAt { get; init; }

    /// <summary>OAuth scopes the token was granted, when recorded; sent back on refresh.</summary>
    public IReadOnlyList<string>? Scopes { get; init; }

    /// <summary>Informational subscription label, if present.</summary>
    public string? SubscriptionType { get; init; }

    /// <summary>Organization identifier that scopes the usage quota, if present.</summary>
    public string? OrganizationUuid { get; init; }

    /// <summary>Determines whether the access token is expired (or within the skew window).</summary>
    /// <param name="now">The current instant.</param>
    /// <returns><see langword="true"/> if the token should be refreshed before use.</returns>
    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now + ExpirySkew;
}
