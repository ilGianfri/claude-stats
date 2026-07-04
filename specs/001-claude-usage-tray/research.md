# Phase 0 Research: Claude Usage Tray

**Date**: 2026-07-04 | **Feature**: 001-claude-usage-tray

This document resolves the technical unknowns from the plan's Technical Context. Findings are labeled
**CONFIRMED** (official docs or read directly from the shipped Claude Code binary / the local
machine), **LIKELY** (multiple independent community sources agree), or **UNVERIFIED** (plausible,
not empirically proven here).

---

## 1. Source of truth for current usage & limit reset

**Decision**: Obtain current usage and reset times from the authenticated endpoint
`GET https://api.anthropic.com/api/oauth/usage`, reusing the OAuth access token stored by Claude Code.

**Rationale**: The subscription rate-limit windows (rolling 5-hour and weekly, with % consumed and
reset timestamps) are **not** stored anywhere locally — verified on this machine: `~/.claude` contains
no cached rate-limit JSON, and `policy-limits.json` is policy configuration, not usage. The only way
to get the same numbers Claude Code's `/usage` shows is this endpoint. This is the internal
`fetchUtilization` call that powers `/usage` (**CONFIRMED** from the shipped binary).

**Request** (all headers CONFIRMED except where noted):

| Header | Value |
|--------|-------|
| `Authorization` | `Bearer <claudeAiOauth.accessToken>` |
| `anthropic-beta` | `oauth-2025-04-20` |
| `Content-Type` | `application/json` |
| `User-Agent` | `claude-code/<version>` — **effectively required**: without it the endpoint returns persistent HTTP 429 with no `Retry-After` (LIKELY, strongly supported by community reports). |

- Method **GET**, 5 s timeout, automatic 401 → refresh → retry.

**Response shape** (field names/semantics CONFIRMED from the binary parser; JSON envelope UNVERIFIED):

```jsonc
{
  "five_hour":        { "utilization": 0.42, "resets_at": 1751648400 }, // rolling ~5h window
  "seven_day":        { "utilization": 0.13, "resets_at": 1752080400 }, // weekly window
  "seven_day_opus":   { "utilization": 0.20, "resets_at": 1752080400 }, // weekly Opus sub-limit
  "seven_day_sonnet": { "utilization": 0.05, "resets_at": 1752080400 },
  "seven_day_oauth_apps": { "utilization": 0.0, "resets_at": 1752080400 },
  "extra_usage":      { /* overage / extra-credit balance */ },
  "cinder_cove":      { /* reserved */ }
}
```

- `utilization` is a **0–1 float** → percent = `utilization * 100`.
- `resets_at` is **Unix epoch seconds** → local time = `DateTimeOffset.FromUnixTimeSeconds(resets_at)`.
- Map: `five_hour` → rolling window; `seven_day` → weekly window (spec FR-002/FR-003). `seven_day_opus`
  may be surfaced as an extra window.
- **Defensive parsing required**: a `limits[]` array variant (objects with `utilization`, `resets_at`,
  `percent`, `scope.model.display_name`) has been observed in some versions (LIKELY). Tolerate missing
  windows and both shapes; never hard-fail on an absent field.

**Alternatives considered**:
- *Rate-limit response headers on `/v1/messages`* (`anthropic-ratelimit-unified-5h-utilization`,
  `-5h-reset`, `-7d-*`, `-representative-claim`, `-status`) — CONFIRMED to carry the same data and to
  apply to OAuth-subscription auth, but they only arrive as a side effect of spending quota on a real
  inference call. Rejected as the primary source (the endpoint reads usage without spending); retained
  as a documented fallback/cross-check.
- *Local-only estimation from transcripts* — cannot produce the true subscription % or reset time
  (server-side). Rejected for current-usage; used for historical stats instead (see §3).

**Risk**: The endpoint, the `oauth-2025-04-20` beta gate, and the OAuth client id are **unofficial and
undocumented**; Anthropic has an open request to provide a supported usage command. Stability risk is
moderate–high (field names and hostnames have already shifted). Mitigation: isolate all of this behind
`IUsageClient`, parse defensively, degrade to a clear "unavailable" state, and keep polling ≤ 1/min.

---

## 2. Credential location & OAuth token refresh

**Decision**: Read credentials from `%USERPROFILE%\.claude\.credentials.json`; refresh the token only
when expired via `POST https://platform.claude.com/v1/oauth/token`.

**Credential file** (CONFIRMED on this machine — see [contracts/credentials-file.md](./contracts/credentials-file.md)):
`claudeAiOauth.{ accessToken, refreshToken, expiresAt (epoch ms), subscriptionType, rateLimitTier }`
and `organizationUuid`. Read with `FileShare.ReadWrite` (the CLI may rewrite it), tolerate a
mid-write partial/empty read as transient.

**Refresh** (CONFIRMED from the binary):

```
POST https://platform.claude.com/v1/oauth/token
Content-Type: application/json
anthropic-beta: oauth-2025-04-20
User-Agent: claude-code/<version>

{ "grant_type": "refresh_token",
  "refresh_token": "<refreshToken>",
  "client_id": "9d1c250a-e61b-44d9-88ed-5944d1962f5e" }
```

Response returns a new `access_token`, a **rotated** `refresh_token`, and `expires_in` (seconds).
Persist back under `claudeAiOauth`, setting `expiresAt = now + expires_in*1000`.

**Rationale**: Access tokens live only hours; without refresh the app would be unable to read usage
most of the time. `client_id` and URLs are CONFIRMED constants from the shipped binary.

**Constraints / mitigations**:
- **Refresh-token rotation** — always store the returned refresh token or the next refresh breaks.
- **Coexistence with a running Claude Code** — both processes may write `.credentials.json`, and
  rotation means concurrent refreshes can invalidate each other. Mitigation: refresh only when
  `expiresAt` has passed (with ~60 s skew) or on a 401; **re-read the file immediately before
  writing**; write atomically (temp file + move) with `FileShare.ReadWrite`.
- **Never log token values.** Refreshing writes the credentials file (not usage data) — consistent
  with FR-013's read-only-on-*usage* intent; documented explicitly.
- Cloudflare WAF may block refresh from datacenter IPs (irrelevant for a residential desktop; treated
  as a transient failure → "sign-in required" state).

**Alternative considered**: never refresh; show "sign in to Claude required" whenever the token is
expired and let Claude Code refresh. Rejected — the app would be dark for hours at a time. Kept as the
graceful fallback when refresh itself fails.

---

## 3. Historical usage statistics from local transcripts

**Decision**: Aggregate per-day stats by scanning `%USERPROFILE%\.claude\projects\**\*.jsonl`.

**Schema** (CONFIRMED on this machine — see [contracts/transcript-usage.md](./contracts/transcript-usage.md)):
each line is a JSON record; assistant records have `type == "assistant"`, `timestamp` (ISO 8601),
`message.model` (e.g. `claude-opus-4-8`), and `message.usage` with `input_tokens`, `output_tokens`,
`cache_creation_input_tokens`, `cache_read_input_tokens` (plus nested detail).

**Aggregation**: bucket assistant records by local calendar day over the last ~30 days; per day sum
tokens (by type) and count messages; compute estimated cost via the pricing map (§4). Roll up totals
for the period. Message count = assistant records (user-prompt count available via `type=="user"` if
wanted).

**Performance**: 145 files today and growing. Do the scan **off the UI thread** on demand (trend view
open) and on a background cadence; read files with `FileShare.ReadWrite`; process line-by-line
(streaming, not whole-file loads). Optimization (deferred unless needed): cache results and re-read
only files whose modified-time changed since the last scan.

**Rationale**: This is the only local source rich enough for a 30-day trend, and it needs no
authentication. Independent of §1 — the P1 headline works even if this is empty, and vice versa
(FR-016).

**Alternatives considered**: shelling out to the `ccusage` tool — rejected (extra runtime dependency;
we can read the same JSONL directly). Using the usage endpoint for history — it only exposes current
windows, not 30-day history.

---

## 4. Estimated cost model

**Decision**: Bundle a static per-model price map (`IPricingProvider`) keyed by model id, with rates
per million tokens for input, output, cache-write (5m/1h), and cache-read; compute
`cost = Σ(tokens_by_type × rate_by_type)`.

**Rationale**: Cost is not recorded in transcripts; it must be derived. A bundled map keyed by
`message.model` is simple, offline, and testable. Cache-read is ~0.1× input and cache-write ~1.25×
(5m)/2× (1h) input per Anthropic's published pricing model (**LIKELY** — exact current rates to be
filled from official pricing at implementation and kept in one editable table).

**Constraints**: label the figure "estimated"; unknown model ids fall back to a conservative default
or are shown as "n/a" rather than silently zero. Keep the table in one place for easy updates.

**Alternative considered**: omit cost entirely (tokens/messages only). Rejected — the spec's stats
scope (clarified) includes estimated cost; kept best-effort.

---

## 5. WPF tray-app technical stack

**Decision** (all versions checked on NuGet 2026-07-04; .NET 10 is GA/LTS):

| Concern | Choice | Version | Why |
|---------|--------|---------|-----|
| Tray icon | `H.NotifyIcon.Wpf` | 2.4.1 | Only tray lib with an explicit `net10.0-windows7.0` target; actively maintained; MVVM-friendly (`LeftClickCommand`, bindable `IconSource`/`ToolTipText`, XAML `ContextMenu`); dynamic icon swap for warning/reached state. |
| MVVM | `CommunityToolkit.Mvvm` | 8.4.2 | Mandated by constitution. |
| Host/DI | `Microsoft.Extensions.Hosting` | 10.0.* | Generic Host, DI, `ILogger`, config; `BackgroundService` for polling. |
| HTTP | `Microsoft.Extensions.Http` | 10.0.* | `IHttpClientFactory` typed clients. |
| JSON | `System.Text.Json` | in-box | Source generation (`AppJsonContext`) for consumed contracts. |
| Chart | Hand-rolled `ItemsControl`+`Rectangle` | — | Zero deps, themeable, honors lightweight goal (SC-006); binds to `ObservableCollection<DailyUsage>`. |
| Autostart | `Microsoft.Win32.Registry` | in-box | HKCU `...\Run` (per-user, no admin, easy to read/toggle). |
| Tests | `xunit.v3` + runner + Test SDK, `NSubstitute` | 3.2.2 / current | ViewModels/services testable with no UI. |

**Bootstrap pattern**: remove `StartupUri`; set `ShutdownMode="OnExplicitShutdown"`; in
`App.OnStartup` build `Host.CreateApplicationBuilder()`, register services, `await host.StartAsync()`,
then bind the `TaskbarIcon` (declared in `App.xaml` resources) to `TrayViewModel` and `ForceCreate()`.
On exit, `Dispose()` the tray icon (or a ghost icon lingers) and `StopAsync()` the host.

**Polling pattern**: `PollingService : BackgroundService` uses `PeriodicTimer` (non-reentrant,
allocation-free) at the configured interval (default 60 s); on each tick calls `IUsageClient`, then
marshals the result to the UI thread via `IUiDispatcher` and broadcasts `UsageUpdatedMessage` through
`IMessenger`; on error broadcasts `UsageErrorMessage`. Honors the host `stoppingToken`.

**UI-thread abstraction**: `IUiDispatcher.InvokeAsync(Action)` (WPF impl wraps
`Application.Current.Dispatcher`; test impl runs inline) keeps `System.Windows` out of ViewModels so
the test project can target plain `net10.0` (Constitution IV).

**Resilience**: skip `Microsoft.Extensions.Http.Resilience` for v1 — the `PeriodicTimer` loop is a
natural retry and a `try/catch` surfaces a warning state; just set `HttpClient.Timeout`. Add standard
resilience later if the interactive one-shot calls prove flaky.

**Alternatives considered**: `Hardcodet.NotifyIcon.Wpf` (dormant, no net10 target) and WinForms
`NotifyIcon` (no XAML/binding, drags in WinForms) — both rejected for MVVM fit. `LiveCharts2`
(SkiaSharp native deps + RID publish) and `ScottPlot`/`OxyPlot` — rejected for v1 in favor of a
dependency-free bar chart, documented as the upgrade path.

---

## 6. Supporting decisions

- **Single instance**: named `Mutex` (`Local\ClaudeStats.SingleInstance`) at startup; if not the
  creator, exit. Keep the mutex referenced for process lifetime. Optional named `EventWaitHandle` to
  surface the existing instance's popup. **Rationale**: a tray utility must not stack duplicate icons.
- **Autostart**: write/remove `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\ClaudeStats` =
  quoted `Environment.ProcessPath` (correct for single-file publish; `Assembly.Location` is empty
  there). User-toggleable in Settings (FR-012).
- **Reference clock**: `IClock` abstraction so reset countdowns and "day" bucketing are testable and
  correct across the reset boundary (SC-007) and time zones.
- **Icon states**: three `.ico` (normal / warning ≥80% / reached 100%) swapped via the bound
  `IconSource`; state derived from the most-constraining window (FR-011).
- **Agent context script**: `.specify/scripts/powershell/` has no `update-agent-context` script in
  this Spec Kit version, so that Phase 1 step is skipped (no CLAUDE.md generated by this command).

**All NEEDS CLARIFICATION items are resolved.** No open unknowns block Phase 1 design; the only
residual risk is the unofficial endpoint's stability, mitigated by isolation + defensive parsing.
