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
| **429 (rate_limit_error)** | **VERIFIED against the live endpoint (2026-07-10):** JSON body `{"error":{"type":"rate_limit_error","message":"Rate limited. Please try again later."}}`, served via Cloudflare (`server=cloudflare`), **no `Retry-After`**. The token endpoint punishes *frequency*: it expects refreshes to be rare. Enter a long cooldown (see trigger policy) — never retry on the next poll. |
| network/timeout | transient → retry next cycle |
| concurrent write race | last-writer-wins is acceptable; re-read-before-write minimizes it |

## Trigger policy (hybrid — defer to Claude Code, refresh rarely)
The refresh token is **shared with Claude Code and rotates on every refresh**, so two clients that
both refresh duel over it and storm the rate-limited token endpoint. Therefore:

1. Refresh proactively when `IsExpired` (`ExpiresAt <= now + 60s`), or reactively on a single `401`.
2. **Re-read `.credentials.json` first.** If the shared file already holds a fresh, *different*
   token (Claude Code refreshed it), use that and do **not** call the token endpoint.
3. Only when the token is still stale do we POST a refresh, and at most **once per cooldown window**.
4. A failed refresh arms an exponential cooldown (base **15 min**, cap **4 h**, +20% jitter). While
   cooling down, polls return `Stale`/last-known without touching the token endpoint.
5. Do **not** refresh on every poll. (Implemented in `UsageClient.RefreshWithCooldownAsync`.)

> **Known limitation:** the cooldown is per-process, so repeatedly relaunching the app while
> rate-limited re-triggers one immediate refresh per launch. Not an issue for the auto-start path.
