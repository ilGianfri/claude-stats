# Contract (consumed): OAuth Token Refresh

**Status**: Unofficial / undocumented (constants read from the shipped Claude Code binary). Behind
`IOAuthTokenService`. Invoked only when the access token is expired or a `401` occurs.

## Request

```
POST https://platform.claude.com/v1/oauth/token
Content-Type: application/json          # no charset parameter
Accept: application/json, text/plain, */*
User-Agent: axios/<version>              # what Claude Code's own (axios) client sends

{
  "grant_type":    "refresh_token",
  "refresh_token": "<claudeAiOauth.refreshToken>",
  "client_id":     "9d1c250a-e61b-44d9-88ed-5944d1962f5e",
  "scope":         "user:inference user:profile user:sessions:claude_code user:mcp_servers user:file_upload"
}
```

`scope` is **required**: it is the stored `claudeAiOauth.scopes` joined by spaces, or Claude Code's
default scope list when the file has none. **Do not** send the usage-endpoint headers
(`User-Agent: claude-code/*`, `anthropic-beta`): verified 2026-09-11 against the live endpoint, a
refresh that omits `scope` *or* carries those headers is answered `429 rate_limit_error` on every
attempt, regardless of how rarely it is sent (two months of field logs: 0 successes), while the
shape above succeeds immediately with the same refresh token.

## Response (200)

```jsonc
{
  "token_type":               "Bearer",
  "access_token":             "<new access token>",
  "refresh_token":            "<ROTATED refresh token>",
  "expires_in":               28800,      // seconds (8 h observed)
  "refresh_token_expires_in": 519166,     // seconds (~6 days observed; renewed on every refresh)
  "scope":                    "user:inference user:profile ...",
  "token_uuid": "...", "organization": { ... }, "account": { ... }
}
```

## Persistence rules
1. Compute `expiresAt = now + expires_in * 1000` (epoch ms).
2. **Re-read `.credentials.json` immediately before writing** (the CLI may have rotated it).
3. Update `claudeAiOauth.{ accessToken, refreshToken, expiresAt, refreshTokenExpiresAt, scopes }`
   only; preserve all other keys (`mcpOAuth`, `organizationUuid`, `rateLimitTier`, …).
4. Write atomically: temp file in the same directory → `File.Move`/replace; open with
   `FileShare.ReadWrite`.
5. **Always store the rotated `refresh_token`** or the next refresh fails.
6. **Never log token values.**

## Error handling
| Condition | Behavior |
|-----------|----------|
| 400/401 (invalid_grant) | refresh failed → `Unavailable` ("sign in to Claude / run Claude Code") |
| 403 (Cloudflare/WAF) | transient → `Stale`/`Unavailable`; retry next cycle |
| **429 (rate_limit_error)** | JSON body `{"error":{"type":"rate_limit_error","message":"Rate limited. Please try again later."}}`, via Cloudflare, **no `Retry-After`**. In practice this is the endpoint's answer to a **malformed refresh** (missing `scope`, usage-style headers) far more often than to real frequency. Still treated as transient with a long cooldown (see trigger policy) so a genuine limit is never stormed. |
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

## Storage note (Claude Code ≥ 2.1.265 on Windows)
Claude Code now keeps its credentials in the **Windows Credential Manager** (`Bun.secrets`,
chunked) and writes `.credentials.json` only as a plaintext fallback, so the file is **no longer
refreshed by Claude Code** on such machines. ClaudeStats therefore owns the file's token chain:
it refreshes every ~8 h and the refresh token is renewed (~6 days) on each refresh, so the chain
stays alive as long as the app runs at least once a week. If the chain dies the user must produce
a fresh file (e.g. `claude` CLI with `CLAUDE_CODE_FORCE_WINDOWS_CREDMAN` unset / a build that
writes the file) or ClaudeStats needs its own sign-in — see follow-ups.
