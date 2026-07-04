namespace ClaudeStats.Services;

/// <summary>
/// Constants for the (unofficial) Claude usage and OAuth endpoints, verified from the shipped
/// Claude Code binary. Isolated here so a change to the wire contract touches one place.
/// </summary>
internal static class ClaudeApi
{
    /// <summary>Base address for the Anthropic API host.</summary>
    public const string ApiBaseUrl = "https://api.anthropic.com/";

    /// <summary>Relative path of the usage endpoint (against <see cref="ApiBaseUrl"/>).</summary>
    public const string UsagePath = "api/oauth/usage";

    /// <summary>Base address for the OAuth token host.</summary>
    public const string PlatformBaseUrl = "https://platform.claude.com/";

    /// <summary>Relative path of the OAuth token refresh endpoint.</summary>
    public const string TokenPath = "v1/oauth/token";

    /// <summary>OAuth client id used by Claude Code.</summary>
    public const string OAuthClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";

    /// <summary>Beta gate header value required by the OAuth surfaces.</summary>
    public const string AnthropicBeta = "oauth-2025-04-20";

    /// <summary>User-Agent required to avoid the aggressive anti-abuse rate-limit bucket.</summary>
    public const string UserAgent = "claude-code/2.1.85";
}
