using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeStats.Json;

/// <summary>Wire model for the <c>claudeAiOauth</c> object inside the credentials file.</summary>
public sealed class ClaudeAiOAuthDto
{
    /// <summary>Bearer access token.</summary>
    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }

    /// <summary>Refresh token.</summary>
    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }

    /// <summary>Expiry as Unix epoch milliseconds.</summary>
    [JsonPropertyName("expiresAt")]
    public long ExpiresAt { get; set; }

    /// <summary>Refresh-token expiry as Unix epoch milliseconds (written by newer Claude Code builds).</summary>
    [JsonPropertyName("refreshTokenExpiresAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? RefreshTokenExpiresAt { get; set; }

    /// <summary>OAuth scopes granted to the token.</summary>
    [JsonPropertyName("scopes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Scopes { get; set; }

    /// <summary>Subscription label.</summary>
    [JsonPropertyName("subscriptionType")]
    public string? SubscriptionType { get; set; }

    /// <summary>Rate-limit tier label.</summary>
    [JsonPropertyName("rateLimitTier")]
    public string? RateLimitTier { get; set; }

    /// <summary>Any other members preserved verbatim across writes.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>
/// Wire model for <c>~/.claude/.credentials.json</c>. Unknown top-level members
/// (e.g. <c>mcpOAuth</c>) are captured in <see cref="Extra"/> and preserved on write.
/// </summary>
public sealed class CredentialsFileDto
{
    /// <summary>The Claude.ai OAuth credentials block.</summary>
    [JsonPropertyName("claudeAiOauth")]
    public ClaudeAiOAuthDto? ClaudeAiOauth { get; set; }

    /// <summary>Organization identifier scoping the usage quota.</summary>
    [JsonPropertyName("organizationUuid")]
    public string? OrganizationUuid { get; set; }

    /// <summary>Any other top-level members preserved verbatim across writes.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
