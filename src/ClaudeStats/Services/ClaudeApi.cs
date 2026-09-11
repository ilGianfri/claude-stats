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

    /// <summary>Beta gate header value required by the usage endpoint.</summary>
    public const string AnthropicBeta = "oauth-2025-04-20";

    /// <summary>User-Agent the usage endpoint expects (avoids the aggressive anti-abuse rate-limit bucket).</summary>
    public const string UserAgent = "claude-code/2.1.185";

    /// <summary>
    /// User-Agent sent to the token endpoint. Claude Code performs its own refresh through a plain
    /// axios client (no claude-code UA, no anthropic-beta header), and the token endpoint answers
    /// 429 rate_limit_error to the usage-style headers regardless of frequency, so the refresh
    /// request mirrors what Claude Code actually sends.
    /// </summary>
    public const string TokenUserAgent = "axios/1.8.4";

    /// <summary>
    /// OAuth scopes Claude Code requests when it refreshes; the token endpoint rejects a refresh
    /// that omits the scope field. Used when the credentials file carries no scopes array.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultOAuthScopes =
    [
        "user:inference",
        "user:profile",
        "user:sessions:claude_code",
        "user:mcp_servers",
        "user:file_upload",
    ];
}
