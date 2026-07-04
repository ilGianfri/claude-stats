# Implementation Plan: Claude Usage Tray

**Branch**: `001-claude-usage-tray` | **Date**: 2026-07-04 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-claude-usage-tray/spec.md`

## Summary

A resident Windows system-tray utility that shows, at a glance, the user's current Claude
subscription usage and when the limit resets, with an optional 30-day usage-statistics view. Current
usage and reset times come from an authenticated Anthropic endpoint (`GET /api/oauth/usage`) reached
by reusing the local Claude Code OAuth token in `%USERPROFILE%\.claude\.credentials.json`; historical
statistics are aggregated from local Claude transcript files (`~/.claude/projects/**/*.jsonl`). The
app is built as WPF on .NET 10 following the project constitution (MVVM with CommunityToolkit.Mvvm,
DI, testable UI-agnostic ViewModels, responsive async), hosted on the .NET Generic Host with a
background polling service and a tray icon that changes state at 80% / 100% of the most-constraining
limit window.

## Technical Context

**Language/Version**: C# 13 on .NET 10 (`net10.0-windows`); app project already scaffolded at
`src/ClaudeStats`.

**Primary Dependencies**:
- WPF (`UseWPF=true`) — presentation shell (already enabled).
- `CommunityToolkit.Mvvm` 8.4.2 — MVVM (mandated by constitution).
- `H.NotifyIcon.Wpf` 2.4.1 — system tray icon (only tray lib with an explicit `net10.0-windows` target; MVVM/binding friendly, dynamic icon state).
- `Microsoft.Extensions.Hosting` 10.0.* + `Microsoft.Extensions.Http` 10.0.* — Generic Host, DI, `IHttpClientFactory` typed clients, background service.
- `System.Text.Json` (in-box) with source generation for the consumed JSON contracts.
- `Microsoft.Win32.Registry` (in-box) — per-user autostart via HKCU Run key.
- 30-day trend chart: **hand-rolled WPF `ItemsControl` + `Rectangle` bar chart** (zero extra deps, themeable, honors the lightweight goal). `LiveChartsCore.SkiaSharpView.WPF` 2.0.5 is the documented upgrade path if richer visuals are wanted later.
- Tests: `xunit.v3` 3.2.2 (+ `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`), `NSubstitute` for mocking.

**Storage**: No database. Reads two local sources: the OAuth credentials file
(`%USERPROFILE%\.claude\.credentials.json`) and Claude transcript JSON-lines files under
`%USERPROFILE%\.claude\projects\`. Writes only: the app's own settings (autostart, refresh interval)
and, when the access token is expired, a careful refreshed-token write back to the credentials file
(see Constraints).

**Testing**: `dotnet test` with xUnit v3; ViewModels and services tested with no UI by mocking their
injected interfaces. Test project targets plain `net10.0` (no `-windows`) — enforced by keeping
`System.Windows` out of ViewModels/services.

**Target Platform**: Windows 10/11 desktop, x64/arm64.

**Project Type**: Desktop application (single WPF app project + one unit-test project).

**Performance Goals**: Tray tooltip/popup reflects cached data with no perceptible delay
(< 100 ms interaction); near-zero CPU when idle; modest memory footprint (target < ~100 MB working
set) so users leave it running all day (SC-006). Usage auto-refresh ≤ 60 s (SC-002).

**Constraints**:
- Poll the usage endpoint **no more than once per minute** and back off on HTTP 429 — the endpoint is
  aggressively rate-limited when the `User-Agent: claude-code/<version>` header is missing/abused.
- Read-only with respect to Claude *usage data* (FR-013). The only permitted credential-file write is
  a token refresh, performed with `FileShare.ReadWrite`, re-reading the file immediately before
  writing, and never logging token values.
- Offline/failure tolerant: each data source can be unavailable independently; the app shows a clear
  status and last-known-good timestamp instead of blank/misleading values (FR-008, SC-004).
- The usage endpoint, `anthropic-beta: oauth-2025-04-20` gate, and OAuth client id are **unofficial /
  undocumented** and may change → all parsing is defensive and isolated behind an interface.

**Scale/Scope**: Single user, single Claude account (v1). ~3 small windows/views, ~8 services,
~5 domain models. Transcript corpus grows over time (145 files at planning time); the 30-day
aggregation must scan efficiently (incremental read of only recently modified files; work done off
the UI thread).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Constitution v1.0.0. All five principles are satisfied by this design; no violations, so Complexity
Tracking is empty.

| Principle | Gate | Status |
|-----------|------|--------|
| I. MVVM Separation (NON-NEGOTIABLE) | Views hold only markup; ViewModels expose bindable state + commands and reference no WPF element types; Models are domain-only | ✅ PASS — `Views/` (tray icon in `App.xaml`, popup, trend window, settings) bind to `ViewModels/` (`TrayViewModel`, `TrendViewModel`, `SettingsViewModel`); domain in `Models/`; data access in `Services/`. |
| II. CommunityToolkit.Mvvm First | `ObservableObject`/`ObservableRecipient`, `[ObservableProperty]`, `[RelayCommand]`; `IMessenger` for cross-component | ✅ PASS — ViewModels derive from toolkit base types; polling→UI updates broadcast via `IMessenger` (`UsageUpdatedMessage`) rather than direct references; no hand-rolled `INotifyPropertyChanged`/`ICommand`. |
| III. Data Binding Over Code-Behind | Interactions via `Command`/binding; code-behind = `InitializeComponent()` + view-only concerns | ✅ PASS — tray clicks, menu items, refresh, exit, autostart toggle are commands; code-behind limited to host bootstrap in `App.xaml.cs` and view-only glue. |
| IV. Testable, UI-Agnostic ViewModels | Dependencies via constructor-injected interfaces; no `System.Windows`/`Dispatcher` in VMs; unit tests for non-trivial VMs | ✅ PASS — `IUsageClient`, `ICredentialStore`, `IOAuthTokenService`, `IUsageStatsReader`, `IPricingProvider`, `IAutostartService`, `IUiDispatcher`, `IClock` are injected; dispatcher marshaling behind `IUiDispatcher`; test project is `net10.0`. |
| V. Responsive Async UI | Async I/O off the UI thread; `[RelayCommand]` async + CanExecute; no `.Result`/`.Wait()`/`Thread.Sleep` on UI thread; honor `CancellationToken` | ✅ PASS — `PollingService : BackgroundService` uses `PeriodicTimer`; HTTP/file work is async; commands use `AsyncRelayCommand`; cancellation flows from host `stoppingToken`. |

**Technology & Platform**: `net10.0-windows` + `UseWPF` ✅ (already set); `Nullable` + `ImplicitUsings`
enabled ✅ (already set); explicit types instead of `var` ✅ (enforced in review); WPF +
CommunityToolkit.Mvvm ✅. No `alebro.<servicename>` shared library is required for this standalone
utility; if the credential/OAuth reader later needs sharing it can be promoted to one (noted, not a
violation).

**Development Workflow & Quality Gates**: every new method gets a one-line `<summary>` + `<param>`/
`<returns>` ✅; builds must be warning-clean ✅; ViewModel logic ships with unit tests ✅; this
Constitution Check gate passes before implementation ✅.

**Post-Design re-check (after Phase 1)**: Re-evaluated against `data-model.md` and `contracts/` — the
entities are plain domain models, the service interfaces keep `System.Windows` out of the testable
layer, and messaging uses `IMessenger`. **Still PASS; no new violations; Complexity Tracking remains
empty.**

## Project Structure

### Documentation (this feature)

```text
specs/001-claude-usage-tray/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (data source, endpoint, tech-stack decisions)
├── data-model.md        # Phase 1 output (entities, states, validation)
├── quickstart.md        # Phase 1 output (build/run/validate guide)
├── contracts/           # Phase 1 output (consumed integration contracts + internal service interfaces)
│   ├── usage-endpoint.md
│   ├── oauth-token-refresh.md
│   ├── credentials-file.md
│   ├── transcript-usage.md
│   └── internal-services.md
├── checklists/
│   └── requirements.md  # Spec quality checklist (from /speckit-specify + /speckit-clarify)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/
└── ClaudeStats/                       # WPF app (net10.0-windows) — already scaffolded
    ├── ClaudeStats.csproj             # add packages; remove StartupUri usage
    ├── App.xaml / App.xaml.cs         # Generic Host bootstrap, tray icon, ShutdownMode=OnExplicitShutdown
    ├── Views/
    │   ├── TrayPopupView.xaml         # compact usage/reset panel (per-window list)
    │   ├── TrendWindow.xaml           # 30-day statistics view (hand-rolled bar chart)
    │   └── SettingsView.xaml          # autostart + refresh interval (optional surface)
    ├── ViewModels/
    │   ├── TrayViewModel.cs           # headline usage, reset, icon state, commands
    │   ├── TrendViewModel.cs          # per-day series + totals
    │   └── SettingsViewModel.cs
    ├── Models/
    │   ├── UsageSnapshot.cs
    │   ├── LimitWindow.cs             # enum WindowKind { FiveHour, Weekly, WeeklyOpus, ... }
    │   ├── LimitState.cs              # enum { Normal, Warning, Reached, Unavailable, Stale }
    │   ├── UsageStatistics.cs / DailyUsage.cs
    │   ├── OAuthCredentials.cs
    │   └── ModelPrice.cs
    ├── Services/
    │   ├── IUsageClient.cs / UsageClient.cs                 # GET /api/oauth/usage (typed HttpClient)
    │   ├── ICredentialStore.cs / CredentialStore.cs        # read/write .credentials.json
    │   ├── IOAuthTokenService.cs / OAuthTokenService.cs     # refresh when expired
    │   ├── IUsageStatsReader.cs / TranscriptUsageStatsReader.cs  # aggregate jsonl → DailyUsage[]
    │   ├── IPricingProvider.cs / PricingProvider.cs        # model → per-MTok price map
    │   ├── IAutostartService.cs / RegistryAutostartService.cs
    │   ├── IUiDispatcher.cs / WpfDispatcher.cs
    │   ├── IClock.cs / SystemClock.cs
    │   └── PollingService.cs                                # BackgroundService + PeriodicTimer
    ├── Messages/
    │   ├── UsageUpdatedMessage.cs
    │   └── UsageErrorMessage.cs
    ├── Json/AppJsonContext.cs                               # System.Text.Json source-gen context
    ├── Configuration/AppSettings.cs                         # refresh interval, thresholds, autostart
    └── Resources/icons/                                     # normal / warning / reached .ico

tests/
└── ClaudeStats.Tests/                 # net10.0 (no -windows) unit tests
    ├── ClaudeStats.Tests.csproj
    ├── ViewModels/TrayViewModelTests.cs, TrendViewModelTests.cs, SettingsViewModelTests.cs
    └── Services/UsageClientTests.cs, TranscriptUsageStatsReaderTests.cs,
        OAuthTokenServiceTests.cs, PricingProviderTests.cs, RegistryAutostartServiceTests.cs

ClaudeStats.sln                        # new solution referencing app + test projects
```

**Structure Decision**: Single desktop application (WPF) plus one unit-test project — matches the
constitution's MVVM layering (`Views/` → `ViewModels/` → `Services/` + `Models/`). The existing
`src/ClaudeStats` scaffold is reused: `MainWindow.*` is removed in favor of the tray-first shell, and
`App.xaml`/`App.xaml.cs` are rewritten to host-bootstrap with no `StartupUri`. A new
`tests/ClaudeStats.Tests` project and a top-level `ClaudeStats.sln` are added.

## Complexity Tracking

> No constitution violations — this table is intentionally empty.
