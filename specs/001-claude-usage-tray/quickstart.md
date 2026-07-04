# Quickstart & Validation: Claude Usage Tray

**Date**: 2026-07-04 | **Feature**: 001-claude-usage-tray

How to build, run, and validate the feature end-to-end. Details live in [plan.md](./plan.md),
[data-model.md](./data-model.md), and [contracts/](./contracts/). This is a run/validation guide, not
an implementation guide.

## Prerequisites
- Windows 10/11, .NET 10 SDK.
- Claude Code signed in on this machine, so `%USERPROFILE%\.claude\.credentials.json` exists and
  `~/.claude/projects/**/*.jsonl` transcripts are present.
- To compare against ground truth, run `claude` and use `/usage`.

## Build & run
```powershell
dotnet restore ClaudeStats.sln
dotnet build   ClaudeStats.sln -c Debug
dotnet run --project src/ClaudeStats/ClaudeStats.csproj
```
Expected: no window opens; a **ClaudeStats icon appears in the notification area** (FR-001, FR-014).

## Unit tests
```powershell
dotnet test ClaudeStats.sln
```
Expected: ViewModel + service tests pass with no UI (Constitution IV). Covers snapshot mapping,
most-constraining selection, state transitions at 80%/100%, endpoint 401→refresh→retry, transcript
aggregation, cost estimation, and autostart toggling.

## Validation scenarios (map to spec)

| # | Action | Expected | Traces |
|---|--------|----------|--------|
| 1 | Hover the tray icon | Tooltip shows headline usage % + time-to-reset of the most-constraining window | US1, FR-002/003/004, SC-001 |
| 2 | Click the tray icon | Compact panel lists each window (rolling ~5h + weekly) with its own % and reset — no full window | US1, FR-003/004/009 |
| 3 | Compare against `/usage` in Claude Code | Numbers match within rounding | SC-003 |
| 4 | Wait one refresh interval after usage changes | Displayed values update automatically (≤ 60 s) | FR-005, SC-002 |
| 5 | Trigger manual refresh (menu) | Values refresh immediately | FR-006 |
| 6 | Open the trend view | 30-day per-day tokens/messages/estimated cost as totals + bar trend | US2, FR-007 |
| 7 | Rename/remove `.credentials.json`, refresh | Clear "sign-in required" state; no crash/zeros; stats still shown if transcripts exist | Edge, FR-008/016, SC-004 |
| 8 | Remove/empty transcripts, open trend | "statistics unavailable"; headline usage still works | US2 AC2, FR-016 |
| 9 | Simulate ≥80% (test/mocked snapshot) | Icon → warning state; 100% → reached state | US3, FR-011 |
| 10 | Cross a reset boundary (mocked `IClock`/snapshot) | Usage + countdown roll over to new window, no restart | Edge, SC-007 |
| 11 | Toggle "Launch at startup", check HKCU Run | Registry value added/removed | FR-012 |
| 12 | Launch a second instance | It exits; single tray icon remains | Research §6 |
| 13 | Choose Exit from the menu | Icon removed, process exits cleanly (no ghost icon) | FR-010 |
| 14 | Leave running idle | Near-zero CPU, modest memory | SC-006 |

## Ground-truth check for the endpoint (optional, manual)
Confirm the live contract before/while implementing `IUsageClient`:
```powershell
$c = Get-Content "$env:USERPROFILE\.claude\.credentials.json" | ConvertFrom-Json
Invoke-RestMethod -Method GET "https://api.anthropic.com/api/oauth/usage" -Headers @{
  Authorization  = "Bearer $($c.claudeAiOauth.accessToken)"
  "anthropic-beta" = "oauth-2025-04-20"
  "User-Agent"   = "claude-code/2.1.85"
  "Content-Type" = "application/json"
}
```
Expected: JSON with `five_hour`/`seven_day` objects (`utilization`, `resets_at`). If it 429s, verify
the `User-Agent`. See [contracts/usage-endpoint.md](./contracts/usage-endpoint.md). Do not commit
tokens or captured responses.

## Notes
- Poll ≤ 1/min; do not hammer the endpoint during manual testing.
- The endpoint is unofficial — if the shape has drifted, defensive parsing should degrade gracefully;
  update `IUsageClient` mapping and the contract doc together.
