# Phase 1 Data Model: Claude Usage Tray

**Date**: 2026-07-04 | **Feature**: 001-claude-usage-tray

Domain entities derived from the spec's Key Entities and Functional Requirements. These are plain,
UI-agnostic C# models (Constitution I/IV) living in `src/ClaudeStats/Models/`. Names are indicative;
field types use .NET terms. All members get XML doc comments in implementation.

---

## Enums

### WindowKind
Identifies a limit window from the usage endpoint.
- `FiveHour` — rolling ~5-hour window (`five_hour`)
- `Weekly` — 7-day window (`seven_day`)
- `WeeklyOpus` — weekly Opus sub-limit (`seven_day_opus`) *(optional surface)*
- `Unknown` — unrecognized key (defensive; ignored in headline)

### LimitState
Derived presentation state for the most-constraining window (drives icon + messaging).
- `Normal` — < 80% consumed
- `Warning` — ≥ 80% and < 100%
- `Reached` — = 100%
- `Unavailable` — no usable reading (no credentials / endpoint unreachable)
- `Stale` — last reading older than the staleness threshold

**Transitions** (evaluated each refresh, using `IClock`):
`Normal → Warning` at ≥80%; `Warning → Reached` at 100%; any → `Normal` after the relevant window
resets below 80% (SC-007 rollover happens automatically on the next tick once `resets_at` passes and
the endpoint reports the new window). Successful read clears `Unavailable`/`Stale`; a failed read
keeps the last good snapshot but flips state to `Stale` (if data exists) or `Unavailable` (if none).

---

## Entities

### LimitWindow
One rate-limit window at a point in time (spec: *Limit Window*).

| Field | Type | Notes / Validation |
|-------|------|--------------------|
| `Kind` | `WindowKind` | required |
| `Utilization` | `double` | 0.0–1.0 from endpoint; clamp to [0,1] defensively |
| `Percent` | `double` (computed) | `Utilization * 100`, 0–100 |
| `ResetsAt` | `DateTimeOffset` | from `resets_at` (epoch seconds → local); must be a valid future/past instant |
| `TimeUntilReset` | `TimeSpan` (computed) | `ResetsAt - IClock.Now`; floored at zero |

Rules: a window with an unparseable/missing `resets_at` or `utilization` is dropped, not shown as
zero (FR-008). `Percent` is the comparison key for "most-constraining".

### UsageSnapshot
A complete point-in-time reading of all windows (spec: *Usage Snapshot*).

| Field | Type | Notes |
|-------|------|-------|
| `Windows` | `IReadOnlyList<LimitWindow>` | 0..n; empty ⇒ `Unavailable` |
| `MostConstraining` | `LimitWindow?` (computed) | window with max `Percent`; null if `Windows` empty |
| `State` | `LimitState` | derived from `MostConstraining.Percent` + freshness |
| `CapturedAt` | `DateTimeOffset` | when this reading was obtained |
| `Source` | `enum { Endpoint, Headers }` | provenance (endpoint primary) |

Rules: `MostConstraining` feeds the tray headline (FR-002) and icon state (FR-011); the popup lists
every `Window` (FR-003, FR-009).

### DailyUsage
Aggregated usage for a single calendar day (component of *Usage Statistics*).

| Field | Type | Notes |
|-------|------|-------|
| `Date` | `DateOnly` | local calendar day |
| `InputTokens` | `long` | sum of `input_tokens` |
| `OutputTokens` | `long` | sum of `output_tokens` |
| `CacheCreationTokens` | `long` | sum of `cache_creation_input_tokens` |
| `CacheReadTokens` | `long` | sum of `cache_read_input_tokens` |
| `TotalTokens` | `long` (computed) | sum of the above |
| `MessageCount` | `int` | assistant records that day |
| `EstimatedCost` | `decimal` | from `IPricingProvider`; ≥ 0; "estimated" |

Rules: all counts ≥ 0; days with no activity may be omitted or emitted as zero-filled to keep the
30-slot trend contiguous (renderer choice).

### UsageStatistics
The 30-day roll-up (spec: *Usage Statistics*).

| Field | Type | Notes |
|-------|------|-------|
| `Days` | `IReadOnlyList<DailyUsage>` | ordered ascending; ≤ ~30 entries |
| `PeriodStart` / `PeriodEnd` | `DateOnly` | inclusive window |
| `TotalTokens` | `long` (computed) | Σ `Days` |
| `TotalMessages` | `int` (computed) | Σ `Days` |
| `TotalEstimatedCost` | `decimal` (computed) | Σ `Days` |
| `IsAvailable` | `bool` | false when no local transcripts found (FR-007/FR-016) |

### OAuthCredentials
Parsed from `.credentials.json` (consumed contract; see contracts/credentials-file.md). **Never
logged.**

| Field | Type | Notes |
|-------|------|-------|
| `AccessToken` | `string` | bearer token |
| `RefreshToken` | `string` | rotated on refresh |
| `ExpiresAt` | `DateTimeOffset` | from epoch ms |
| `SubscriptionType` | `string?` | informational |
| `OrganizationUuid` | `string?` | quota scope |
| `IsExpired` | `bool` (computed) | `ExpiresAt <= IClock.Now + skew(60s)` |

### ModelPrice
One row of the pricing map (`IPricingProvider`).

| Field | Type | Notes |
|-------|------|-------|
| `ModelId` | `string` | e.g. `claude-opus-4-8` |
| `InputPerMTok` / `OutputPerMTok` | `decimal` | USD per 1M tokens |
| `CacheWritePerMTok` / `CacheReadPerMTok` | `decimal` | USD per 1M tokens |

### AppSettings
User configuration (persisted to the app's own settings file, not `.claude`).

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `RefreshInterval` | `TimeSpan` | 60 s | must satisfy SC-002 (≤ 60 s) and poll-rate constraint (≥ ~60 s) |
| `LaunchAtStartup` | `bool` | false | mirrors HKCU Run key (FR-012) |
| `WarningThresholdPercent` | `double` | 80 | fixed at 80 for v1 (FR-011); exposed for future |

---

## Relationships

```
UsageSnapshot 1───* LimitWindow          (MostConstraining = max Percent)
UsageStatistics 1───* DailyUsage         (Days over ~30 calendar days)
OAuthCredentials ──used-by──> IUsageClient / IOAuthTokenService
ModelPrice (map) ──used-by──> IUsageStatsReader (to compute DailyUsage.EstimatedCost)
AppSettings ──used-by──> PollingService (interval), IAutostartService (LaunchAtStartup)
```

## Validation summary (traceability)

- Drop malformed windows / never show misleading zeros → FR-008, SC-004.
- `MostConstraining` drives headline + icon → FR-002, FR-011.
- Popup enumerates all windows → FR-003, FR-009.
- `UsageStatistics.IsAvailable` + `Source` independence → FR-007, FR-016.
- `IsExpired` gates refresh; token never logged → §2 research, FR-013 (usage read-only).
- Reset rollover via `IClock` + fresh snapshot → SC-007.
