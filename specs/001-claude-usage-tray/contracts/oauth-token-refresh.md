# Contract (consumed): OAuth Token Refresh

**Status**: Unofficial / undocumented (constants read from the shipped Claude Code binary). Behind
`IOAuthTokenService`. Invoked only when the access token is expired or a `401` occurs.

## Request

```
POST https://platform.claude.com/v1/oauth/token
Content-Type: application/json
anthropic-beta: oauth-2025-04-20
User-Agent: claude-code/<version>

{
  "grant_type": "refresh_token",
  "refresh_token": "<claudeAiOauth.refreshToken>",
  "client_id": "9d1c250a-e61b-44d9-88ed-5944d1962f5e"
}
```

## Response (200)

```jsonc
{
  "access_token":  "<new access token>",
  "refresh_token": "<ROTATED refresh token>",
  "expires_in":    3600        // seconds
}
```

## Persistence rules
1. Compute `expiresAt = now + expires_in * 1000` (epoch ms).
2. **Re-read `.credentials.json` immediately before writing** (the CLI may have rotated it).
3. Update `claudeAiOauth.{ accessToken, refreshToken, expiresAt }` only; preserve all other keys
   (`mcpOAuth`, `organizationUuid`, …).
4. Write atomically: temp file in the same directory → `File.Move`/replace; open with
   `FileShare.ReadWrite`.
5. **Always store the rotated `refresh_token`** or the next refresh fails.
6. **Never log token values.**

## Error handling
| Condition | Behavior |
|-----------|----------|
| 400/401 (invalid_grant) | refresh failed → `Unavailable` ("sign in to Claude / run Claude Code") |
| 403 (Cloudflare/WAF) | transient → `Stale`/`Unavailable`; retry next cycle |
| network/timeout | transient → retry next cycle |
| concurrent write race | last-writer-wins is acceptable; re-read-before-write minimizes it |

## Trigger policy
Refresh proactively when `IsExpired` (`ExpiresAt <= now + 60s`), or reactively on a single `401`
from the usage endpoint. Do **not** refresh on every poll.
