using System.Text.Json.Serialization;

namespace ClaudeStats.Json;

/// <summary>Request body for the OAuth token refresh endpoint.</summary>
public sealed class TokenRefreshRequestDto
{
    /// <summary>OAuth grant type; always <c>refresh_token</c>.</summary>
    [JsonPropertyName("grant_type")]
    public string GrantType { get; set; } = "refresh_token";

    /// <summary>The current refresh token.</summary>
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>The OAuth client id.</summary>
    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = string.Empty;
}

/// <summary>Response body from the OAuth token refresh endpoint.</summary>
public sealed class TokenRefreshResponseDto
{
    /// <summary>The new access token.</summary>
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    /// <summary>The rotated refresh token.</summary>
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    /// <summary>Lifetime of the new access token, in seconds.</summary>
    [JsonPropertyName("expires_in")]
    public long ExpiresIn { get; set; }
}
