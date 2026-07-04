---
description: "Task list for Claude Usage Tray implementation"
---

# Tasks: Claude Usage Tray

**Input**: Design documents from `/specs/001-claude-usage-tray/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Unit tests are **included and required** — the project constitution (Principle IV) mandates
unit tests for every ViewModel/service with non-trivial logic. These are targeted unit tests (mock the
injected interfaces), not full TDD contract/integration suites.

**Organization**: Tasks are grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1 / US2 / US3 (maps to spec.md user stories); omitted for Setup/Foundational/Polish
- Exact file paths are included in each task

## Path Conventions

Desktop app (per [plan.md](./plan.md) Structure Decision): WPF app at `src/ClaudeStats/`, unit tests at
`tests/ClaudeStats.Tests/`, solution `ClaudeStats.sln` at repo root. The `src/ClaudeStats` scaffold
already exists (default WPF app); it is extended in place.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Solution, packages, and test project so everything else can build.

- [X] T001 Create `ClaudeStats.slnx` at repo root referencing `src/ClaudeStats/ClaudeStats.csproj` and `tests/ClaudeStats.Tests/ClaudeStats.Tests.csproj` (.NET 10 defaults to the `.slnx` solution format)
- [X] T002 Add runtime NuGet package references to `src/ClaudeStats/ClaudeStats.csproj`: `CommunityToolkit.Mvvm` 8.4.2, `H.NotifyIcon.Wpf` 2.4.1, `Microsoft.Extensions.Hosting` 10.0.*, `Microsoft.Extensions.Http` 10.0.*
- [X] T003 [P] Create `tests/ClaudeStats.Tests/ClaudeStats.Tests.csproj` targeting `net10.0-windows` (corrected from `net10.0` — required to reference the WPF app assembly; ViewModels stay UI-agnostic via `IUiDispatcher`) with `xunit.v3` 3.2.2, `xunit.runner.visualstudio` 3.1.5, `Microsoft.NET.Test.Sdk` 18.7.0, `NSubstitute` 5.3.0; project reference to `src/ClaudeStats/ClaudeStats.csproj`
- [X] T004 [P] Add `.editorconfig` at repo root enforcing explicit types over `var` (IDE0008 warning) and set `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in both csproj files (Constitution: explicit types + warning-clean)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Composition root and cross-cutting abstractions every user story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 Rewrite `src/ClaudeStats/App.xaml` + `src/ClaudeStats/App.xaml.cs` to bootstrap the .NET Generic Host (`Host.CreateApplicationBuilder`, DI, `ILogger`, config), remove `StartupUri`, set `ShutdownMode="OnExplicitShutdown"`, and start/stop the host in `OnStartup`/`OnExit` (composition root)
- [X] T006 Delete `src/ClaudeStats/MainWindow.xaml` and `src/ClaudeStats/MainWindow.xaml.cs` (tray-first shell; no main window)
- [X] T007 [P] Create `IClock` + `SystemClock` in `src/ClaudeStats/Services/IClock.cs` / `SystemClock.cs`
- [X] T008 [P] Create `IUiDispatcher` + `WpfDispatcher` (wraps `Application.Current.Dispatcher`) in `src/ClaudeStats/Services/IUiDispatcher.cs` / `WpfDispatcher.cs`
- [X] T009 [P] Create `AppSettings` model + load/save (RefreshInterval default 60s, LaunchAtStartup, WarningThresholdPercent=80) in `src/ClaudeStats/Configuration/AppSettings.cs` + `Services/ISettingsStore.cs` / `JsonSettingsStore.cs`
- [X] T010 [P] Add single-instance guard (named `Mutex` `Local\ClaudeStats.SingleInstance`; exit if not creator) in `src/ClaudeStats/App.xaml.cs`
- [X] T011 [P] Create `System.Text.Json` source-gen context `AppJsonContext` in `src/ClaudeStats/Json/AppJsonContext.cs` (empty scaffold; DTOs registered per story)
- [X] T012 Register foundational services + `IMessenger` (WeakReferenceMessenger) + `AppSettings` + `IClock` + `IUiDispatcher` in the DI composition root in `src/ClaudeStats/App.xaml.cs` (depends on T005, T007, T008, T009, T011)

**Checkpoint**: Host boots to an empty tray-less resident process; DI + config + messaging ready.

---

## Phase 3: User Story 1 - Glance at current usage and reset from the tray (Priority: P1) 🎯 MVP

**Goal**: A persistent tray icon showing the current usage % and reset time of the most-constraining
limit window (hover tooltip + click popup listing every window), auto-refreshing on a timer.

**Independent Test**: Run the app; the tray icon appears; hovering shows usage % + time-to-reset;
clicking opens a panel listing the rolling (~5h) and weekly windows; values match Claude Code `/usage`.

### Models (US1)

- [X] T013 [P] [US1] Create `WindowKind` and `LimitState` enums in `src/ClaudeStats/Models/WindowKind.cs` and `src/ClaudeStats/Models/LimitState.cs` (per [data-model.md](./data-model.md))
- [X] T014 [P] [US1] Create `LimitWindow` model (Utilization clamp, Percent, ResetsAt, TimeUntilReset via IClock) in `src/ClaudeStats/Models/LimitWindow.cs`
- [X] T015 [US1] Create `UsageSnapshot` model with `MostConstraining` (max Percent) and `State` derivation (Normal/Warning≥80/Reached=100/Unavailable/Stale) in `src/ClaudeStats/Models/UsageSnapshot.cs` (depends on T013, T014)
- [X] T016 [P] [US1] Create `OAuthCredentials` model (`IsExpired` via IClock, 60s skew) in `src/ClaudeStats/Models/OAuthCredentials.cs`

### Services & data source (US1)

- [X] T017 [US1] Add DTOs + camelCase/snake_case mapping for the usage endpoint and credentials/refresh payloads and register them in `src/ClaudeStats/Json/AppJsonContext.cs` (per [contracts/usage-endpoint.md](./contracts/usage-endpoint.md), [contracts/credentials-file.md](./contracts/credentials-file.md), [contracts/oauth-token-refresh.md](./contracts/oauth-token-refresh.md); depends on T011)
- [X] T018 [P] [US1] Implement `ICredentialStore` + `CredentialStore` (read/atomic-write `%USERPROFILE%\.claude\.credentials.json`, `FileShare.ReadWrite`, preserve non-`claudeAiOauth` keys, tolerate partial reads, never log) in `src/ClaudeStats/Services/ICredentialStore.cs` / `CredentialStore.cs` (depends on T016, T017)
- [X] T019 [P] [US1] Implement `IOAuthTokenService` + `OAuthTokenService` (POST refresh, store rotated refresh token, re-read-before-write) in `src/ClaudeStats/Services/IOAuthTokenService.cs` / `OAuthTokenService.cs` (per [contracts/oauth-token-refresh.md](./contracts/oauth-token-refresh.md); depends on T016, T017)
- [X] T020 [US1] Implement `IUsageClient` + `UsageClient` (typed HttpClient GET `/api/oauth/usage`, headers incl. `User-Agent: claude-code/<ver>` + `anthropic-beta`, 401→refresh→retry once, defensive parse of both response shapes → `UsageSnapshot`, 429/5xx→Stale) in `src/ClaudeStats/Services/IUsageClient.cs` / `UsageClient.cs` (per [contracts/usage-endpoint.md](./contracts/usage-endpoint.md); depends on T015, T017, T018, T019)
- [X] T021 [P] [US1] Create `UsageUpdatedMessage` and `UsageErrorMessage` records in `src/ClaudeStats/Messages/UsageUpdatedMessage.cs` / `UsageErrorMessage.cs`
- [X] T022 [US1] Implement `PollingService : BackgroundService` (`PeriodicTimer` at AppSettings.RefreshInterval, calls `IUsageClient`, marshals via `IUiDispatcher`, broadcasts messages, honors `stoppingToken`, backs off on 429) in `src/ClaudeStats/Services/PollingService.cs` (depends on T020, T021, T008)

### ViewModel & Views (US1)

- [X] T023 [US1] Implement `TrayViewModel : ObservableRecipient` (headline Percent, reset countdown, per-window list, `StatusText` incl. Unavailable/Stale + LastUpdated, `RefreshCommand` (AsyncRelayCommand), `ExitCommand`; receives Usage messages) in `src/ClaudeStats/ViewModels/TrayViewModel.cs` (depends on T015, T021, T007)
- [X] T024 [P] [US1] Add the normal-state tray icon asset `src/ClaudeStats/Resources/icons/normal.ico` (build action Resource)
- [X] T025 [US1] Declare `H.NotifyIcon` `TaskbarIcon` in `src/ClaudeStats/App.xaml` resources bound to `TrayViewModel` (ToolTipText, LeftClickCommand→popup, ContextMenu Refresh/Exit), `ForceCreate()` on startup and `Dispose()` on exit in `App.xaml.cs` (depends on T005, T023, T024)
- [X] T026 [US1] Create `src/ClaudeStats/Views/TrayPopupView.xaml` compact panel listing each window (% + reset countdown) bound to `TrayViewModel` (depends on T023)
- [X] T027 [US1] Register `ICredentialStore`, `IOAuthTokenService`, typed `HttpClient` for `IUsageClient` (base address `https://api.anthropic.com/`, timeout, default UA/beta headers), `PollingService` (AddHostedService), and `TrayViewModel` in the DI composition root in `src/ClaudeStats/App.xaml.cs` (depends on T018, T019, T020, T022, T023)

### Tests (US1)

- [X] T028 [P] [US1] Unit tests for `UsageClient` (response mapping incl. `limits[]` variant, 401→refresh→retry, 429/5xx→Stale, malformed field dropped not zeroed) using a fake `HttpMessageHandler` + mocked `ICredentialStore`/`IOAuthTokenService` in `tests/ClaudeStats.Tests/Services/UsageClientTests.cs` (depends on T020)
- [X] T029 [P] [US1] Unit tests for `CredentialStore` (parse, atomic write preserves other keys) and `OAuthTokenService` (refresh persistence + refresh-token rotation) in `tests/ClaudeStats.Tests/Services/CredentialStoreTests.cs` and `OAuthTokenServiceTests.cs` (depends on T018, T019)
- [X] T030 [P] [US1] Unit tests for `TrayViewModel` (most-constraining selection, countdown via fake `IClock`, Unavailable/Stale display, message handling, RefreshCommand) in `tests/ClaudeStats.Tests/ViewModels/TrayViewModelTests.cs` (depends on T023)

**Checkpoint**: User Story 1 fully functional and independently testable — this is the shippable MVP.

---

## Phase 4: User Story 2 - See detailed usage statistics (Priority: P2)

**Goal**: A trend view (opened from the tray) showing per-day tokens/messages/estimated cost over the
last ~30 days as totals and a simple bar chart, sourced from local transcripts.

**Independent Test**: Open the trend view; a 30-day per-day trend with totals is shown when transcripts
exist; a clear "statistics unavailable" state is shown when they don't — while US1's headline keeps
working either way.

**Note**: Builds on US1's tray shell (adds a menu command); the stats pipeline itself is independent
of US1's data source (FR-016).

### Models (US2)

- [X] T031 [P] [US2] Create `DailyUsage` model (token sums by type, TotalTokens, MessageCount, EstimatedCost) in `src/ClaudeStats/Models/DailyUsage.cs`
- [X] T032 [P] [US2] Create `UsageStatistics` model (Days, period bounds, computed totals, `IsAvailable`) in `src/ClaudeStats/Models/UsageStatistics.cs`
- [X] T033 [P] [US2] Create `ModelPrice` model in `src/ClaudeStats/Models/ModelPrice.cs`

### Services (US2)

- [X] T034 [P] [US2] Implement `IPricingProvider` + `PricingProvider` (bundled per-model price map: input/output/cache-write/cache-read; unknown model → default/n-a) in `src/ClaudeStats/Services/IPricingProvider.cs` / `PricingProvider.cs` (depends on T033)
- [X] T035 [US2] Implement `IUsageStatsReader` + `TranscriptUsageStatsReader` (stream `%USERPROFILE%\.claude\projects\**\*.jsonl` line-by-line, filter `type==assistant`, bucket by local day, sum tokens, count messages, cost via `IPricingProvider`, off UI thread; missing files → `IsAvailable=false`) in `src/ClaudeStats/Services/IUsageStatsReader.cs` / `TranscriptUsageStatsReader.cs` (per [contracts/transcript-usage.md](./contracts/transcript-usage.md); depends on T031, T032, T034)

### ViewModel & Views (US2)

- [X] T036 [US2] Implement `TrendViewModel` (async load on open, `ObservableCollection<DailyUsage>`, totals, `IsAvailable`/unavailable state) in `src/ClaudeStats/ViewModels/TrendViewModel.cs` (depends on T035, T008)
- [X] T037 [US2] Create `src/ClaudeStats/Views/TrendWindow.xaml` with a hand-rolled `ItemsControl` + `Rectangle` bar chart (+ height-normalization `IValueConverter`) and totals, bound to `TrendViewModel` (depends on T036)
- [X] T038 [US2] Add `ShowTrendCommand` to `TrayViewModel` and a "Trend…" item to the tray `ContextMenu` in `src/ClaudeStats/App.xaml` that opens `TrendWindow` (depends on T025, T036)
- [X] T039 [US2] Register `IPricingProvider`, `IUsageStatsReader`, and `TrendViewModel` in the DI composition root in `src/ClaudeStats/App.xaml.cs` (depends on T034, T035, T036)

### Tests (US2)

- [X] T040 [P] [US2] Unit tests for `TranscriptUsageStatsReader` (day bucketing, token sums, skip non-assistant/malformed lines, no files → `IsAvailable=false`) using temp `.jsonl` fixtures in `tests/ClaudeStats.Tests/Services/TranscriptUsageStatsReaderTests.cs` (depends on T035)
- [X] T041 [P] [US2] Unit tests for `PricingProvider` (known/unknown model) and `TrendViewModel` (totals, unavailable state) in `tests/ClaudeStats.Tests/Services/PricingProviderTests.cs` and `tests/ClaudeStats.Tests/ViewModels/TrendViewModelTests.cs` (depends on T034, T036)

**Checkpoint**: User Stories 1 and 2 both work independently.

---

## Phase 5: User Story 3 - Be warned before hitting the limit (Priority: P3)

**Goal**: The tray icon changes to a distinct warning state at ≥80% and a "reached" state at 100% of
the most-constraining window, returning to normal after reset.

**Independent Test**: Feed a ≥80% snapshot (mocked) → icon shows warning; 100% → reached; after the
window resets below 80% → normal.

**Note**: `UsageSnapshot.State` threshold logic already exists from US1 (T015); this story adds the
visual assets and icon-selection wiring.

- [X] T042 [P] [US3] Add `warning.ico` and `reached.ico` assets in `src/ClaudeStats/Resources/icons/` (build action Resource)
- [X] T043 [US3] Map `LimitState` → bound `IconSource` in `TrayViewModel` (Normal/Warning≥80/Reached=100; restore Normal after reset) in `src/ClaudeStats/ViewModels/TrayViewModel.cs` (depends on T023, T042)
- [X] T044 [US3] (Optional) Show a one-time Windows notification when usage first crosses 80% (spec US3 "optionally a notification") in `src/ClaudeStats/ViewModels/TrayViewModel.cs` (depends on T043)
- [X] T045 [P] [US3] Unit tests for icon-state transitions Normal→Warning(80)→Reached(100)→Normal(after reset) in `tests/ClaudeStats.Tests/ViewModels/TrayViewModelStateTests.cs` (depends on T043)

**Checkpoint**: All three user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Autostart (FR-012), resilience/edge polish, and final validation.

- [X] T046 [P] Implement `IAutostartService` + `RegistryAutostartService` (HKCU `...\Run\ClaudeStats` = quoted `Environment.ProcessPath`; enable/disable/query) in `src/ClaudeStats/Services/IAutostartService.cs` / `RegistryAutostartService.cs` (FR-012)
- [X] T047 [P] Implement `SettingsViewModel` (LaunchAtStartup toggle, RefreshInterval) in `src/ClaudeStats/ViewModels/SettingsViewModel.cs` (depends on T046)
- [X] T048 Create `src/ClaudeStats/Views/SettingsView.xaml` + a "Settings…" tray menu item, and register `IAutostartService` + `SettingsViewModel` in DI in `src/ClaudeStats/App.xaml.cs` (depends on T047)
- [X] T049 [P] Unit tests for `RegistryAutostartService` (enable/disable/query) and `SettingsViewModel` in `tests/ClaudeStats.Tests/Services/RegistryAutostartServiceTests.cs` and `tests/ClaudeStats.Tests/ViewModels/SettingsViewModelTests.cs` (depends on T046, T047)
- [X] T050 Handle system resume / notification-area recreation (re-create icon and force a refresh on `SystemEvents.PowerModeChanged`/`SessionSwitch`) in `src/ClaudeStats/App.xaml.cs` (edge case; depends on T025)
- [X] T051 [P] Audit XML doc comments on all new public methods (Constitution) and add a short `README.md` at repo root describing run/build
- [X] T052 Verify lightweight behavior (poll interval ≥ 60s, tray icon disposed on exit, mutex held for process lifetime; spot-check idle CPU/memory) per SC-006
- [X] T053 Execute all 14 validation scenarios in [quickstart.md](./quickstart.md) and record results
- [X] T054 Final warning-clean `dotnet build ClaudeStats.sln -c Release` + full `dotnet test ClaudeStats.sln` green (Constitution quality gate)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Setup — **BLOCKS all user stories**.
- **User Stories (Phase 3-5)**: all depend on Foundational. US1 is the MVP; US2 and US3 build on US1's
  tray shell (a menu item / icon wiring) but their logic is independently testable.
- **Polish (Phase 6)**: depends on the user stories it touches (autostart is independent; T050 needs the tray).

### User Story Dependencies

- **US1 (P1)**: after Foundational. No dependency on other stories.
- **US2 (P2)**: after Foundational. Adds a tray menu command (touches US1 files T025/TrayViewModel); stats pipeline is independent (FR-016).
- **US3 (P3)**: after Foundational. Reuses `UsageSnapshot.State` from US1 (T015) and edits `TrayViewModel` (T023).

### Within Each User Story

- Enums/models before services; services before ViewModels; ViewModels before Views; DI registration after the components exist.
- Unit tests depend on the component under test but are otherwise parallel with each other.

### Parallel Opportunities

- Setup: T003, T004 in parallel (after T001/T002 land the projects).
- Foundational: T007, T008, T009, T010, T011 in parallel; T012 last.
- US1 models T013, T014, T016 in parallel; T021 and T024 in parallel with services; tests T028, T029, T030 in parallel at the end.
- US2 models T031, T032, T033 in parallel; tests T040, T041 in parallel.
- US3: T042 parallel with earlier work; T045 after T043.
- Different user stories can be staffed in parallel once Foundational is done.

---

## Parallel Example: User Story 1

```text
# After Foundational, launch US1 models together:
Task: T013 Create WindowKind + LimitState enums
Task: T014 Create LimitWindow model
Task: T016 Create OAuthCredentials model

# Then the independent services in parallel:
Task: T018 ICredentialStore + CredentialStore
Task: T019 IOAuthTokenService + OAuthTokenService

# Finally the US1 unit tests together:
Task: T028 UsageClient tests
Task: T029 CredentialStore + OAuthTokenService tests
Task: T030 TrayViewModel tests
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 → 4. **STOP & VALIDATE** against `/usage` → 5. Ship the tray MVP (T001–T030).

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. + US1 → glanceable usage/reset (MVP).
3. + US2 → 30-day stats trend.
4. + US3 → near-limit warning states.
5. + Polish → autostart, resume handling, final validation.

### Parallel Team Strategy

After Foundational: Dev A → US1 (critical path), then Dev B → US2 and Dev C → US3 once US1's tray shell
(T023/T025) exists. Autostart (T046–T049) can proceed independently at any point after Setup.

---

## Notes

- [P] = different files, no incomplete-task dependency.
- Every ViewModel/service with logic has a unit test (Constitution IV); test project is `net10.0` (no UI).
- Keep `System.Windows` out of ViewModels/services — marshal via `IUiDispatcher`.
- The usage endpoint is unofficial: keep parsing defensive and isolated in `UsageClient` (see [research.md](./research.md)).
- Never log token values; the only credential-file write is a token refresh.
- Poll ≤ 1/min; commit after each task or logical group; stop at any checkpoint to validate a story.
