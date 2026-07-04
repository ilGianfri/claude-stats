# Contract (consumed): Anthropic OAuth Usage Endpoint

**Status**: Unofficial / undocumented. Fields verified from the shipped Claude Code binary. Parse
defensively; isolate behind `IUsageClient`.

## Request

```
GET https://api.anthropic.com/api/oauth/usage
Authorization: Bearer <claudeAiOauth.accessToken>
anthropic-beta: oauth-2025-04-20
Content-Type: application/json
User-Agent: claude-code/<version>
```

- Method **GET**; timeout ~5 s.
- `User-Agent` is effectively **required** — a missing/foreign UA drops into an aggressive 429 bucket
  with no `Retry-After`.
- On `401`, refresh the token (see [oauth-token-refresh.md](./oauth-token-refresh.md)) once and retry.

## Response (200)

**VERIFIED against the live endpoint (2026-07-05).** Object keyed by window name; each non-null value
is `{ utilization: number(0..100 PERCENT), resets_at: string(ISO 8601), ... }`. Many window keys are
`null`. An `extra_usage`, `spend`, and a `limits[]` array are also present.

> **Important — differs from the earlier binary-derived guess:** `utilization` is a **percentage
> (0–100)**, not a 0–1 fraction, and `resets_at` is an **ISO 8601 timestamp string**, not epoch
> seconds. The parser must match this or deserialization throws.

```jsonc
{
  "five_hour": { "utilization": 40.0, "resets_at": "2026-07-05T02:19:59.734086+00:00",
                 "limit_dollars": null, "used_dollars": null, "remaining_dollars": null },
  "seven_day": { "utilization": 20.0, "resets_at": "2026-07-10T13:59:59.734105+00:00" },
  "seven_day_opus": null, "seven_day_sonnet": null, "seven_day_oauth_apps": null, "cinder_cove": null,
  "extra_usage": { "is_enabled": false, "utilization": null, /* ... */ },
  "limits": [
    { "kind": "session",       "group": "session", "percent": 40, "resets_at": "…", "scope": null, "is_active": true },
    { "kind": "weekly_all",    "group": "weekly",  "percent": 20, "resets_at": "…", "scope": null, "is_active": false },
    { "kind": "weekly_scoped", "group": "weekly",  "percent": 28, "resets_at": "…",
      "scope": { "model": { "display_name": "Fable" } }, "is_active": false }
  ],
  "spend": { "used": { "amount_minor": 0, "currency": "USD", "exponent": 2 }, "percent": 0, "enabled": false }
}
```

**Mapping to domain** (`data-model.md`):
- **`limits[]` is the primary source** (it is the complete list, including dynamic per-model scoped
  limits). Each entry → one `LimitWindow`, with a dynamic `Label` from `kind` + `scope.model.display_name`:
  `session`→"5-hour", `weekly_all`→"Weekly", `weekly_opus`→"Weekly (Opus)",
  `weekly_scoped`→"Weekly (\<model\>)" (e.g. "Weekly (Fable)"), else title-cased `kind`/scope name.
- **Fallback** (only if `limits[]` is absent/empty): the flat-map keys `five_hour`→`FiveHour`,
  `seven_day`→`Weekly`, `seven_day_opus`→`WeeklyOpus`.
- Skip any window whose percent (`percent`/`utilization`) or `resets_at` is `null`.
- Domain uses a 0–1 fraction internally: `LimitWindow.Utilization = percent / 100`; `Percent = Utilization*100`.
- `ResetsAt = DateTimeOffset.Parse(resets_at).ToLocalTime()` (STJ parses the ISO string directly).
- The scoped limits are **dynamic** — their set and model names vary per account/plan, so labels are
  derived at runtime rather than from a fixed enum.

## Error / edge handling

| Condition | App behavior |
|-----------|--------------|
| 401 | refresh once → retry; if still 401 → `Unavailable` ("sign-in required") |
| 429 | back off (skip this tick, widen interval); keep last snapshot as `Stale` |
| 5xx / network / timeout | keep last snapshot as `Stale`; show reason |
| Missing/extra window keys | ignore unknown; drop malformed; never coerce to 0 |
| Empty/no windows parsed | `Unavailable` |

## Cross-check (fallback)
Same data appears on `/v1/messages` responses as `anthropic-ratelimit-unified-5h-utilization`,
`-5h-reset`, `-7d-utilization`, `-7d-reset`, `-representative-claim`, `-status`. Not used as primary
(requires spending quota) — documented for verification only.
