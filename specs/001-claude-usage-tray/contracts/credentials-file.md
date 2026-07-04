# Contract (consumed): Credentials File

**Path**: `%USERPROFILE%\.claude\.credentials.json` (verified present on the target machine).
**Access**: read with `FileShare.ReadWrite`; write only on token refresh (see
[oauth-token-refresh.md](./oauth-token-refresh.md)). Behind `ICredentialStore`.

## Structure (keys only — values are secret, never logged)

```jsonc
{
  "claudeAiOauth": {
    "accessToken":     "string",   // Bearer token for the usage endpoint
    "refreshToken":    "string",   // rotated on refresh
    "expiresAt":       0,          // epoch MILLISECONDS
    "scopes":          ["string"],
    "subscriptionType":"string",   // e.g. informational plan label
    "rateLimitTier":   "string"
  },
  "mcpOAuth": { /* per-MCP-server tokens — PRESERVE untouched on write */ },
  "organizationUuid": "string"     // quota scope (per-org)
}
```

## Mapping to domain
`claudeAiOauth.accessToken/refreshToken/expiresAt` → `OAuthCredentials`
(`ExpiresAt = DateTimeOffset.FromUnixTimeMilliseconds(expiresAt)`); `organizationUuid` retained.

## Edge handling
| Condition | Behavior |
|-----------|----------|
| File missing | `Unavailable` → "sign in to Claude required" |
| Malformed / partial (mid-write) | treat as transient; retry next cycle; do not crash |
| `claudeAiOauth` absent | `Unavailable` |
| Any write | preserve all non-`claudeAiOauth` keys; atomic replace |
