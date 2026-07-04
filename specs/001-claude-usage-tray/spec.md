# Feature Specification: Claude Usage Tray

**Feature Branch**: `001-claude-usage-tray`

**Created**: 2026-07-04

**Status**: Draft

**Input**: User description: "a windows tray app that gives a quick look at the current claude usage, limit reset and, if possible, usage stats"

## Clarifications

### Session 2026-07-04

- Q: What is the usage data source & authentication approach? → A: Hybrid — an authenticated Anthropic usage endpoint (reusing the local Claude credentials/OAuth token on the machine) is the source of truth for current usage and limit reset, while local Claude data files supply richer historical usage statistics.
- Q: Which limit window(s) should the app surface? → A: Both — the tray headline reflects the most-constraining window (closest to its limit); the popup lists each window (rolling ~5h and weekly) with its own usage and reset.
- Q: What is the scope of the usage-statistics view? → A: Daily trend over the last ~30 days — tokens + message counts + estimated cost, shown as totals and a simple trend (per-model/per-project breakdowns are out of scope for v1).
- Q: At what threshold should the near-limit warning trigger? → A: A single warning state at ≥80% of the most-constraining window; "reached" state at 100%.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Glance at current usage and reset from the tray (Priority: P1)

A Claude user working throughout the day wants to know, at a glance and without interrupting their work, how much of their current Claude usage limit they have already consumed and when that limit will reset. They look at (or hover over) a small icon that always sits in the Windows notification area and immediately see the current usage and the time until reset.

**Why this priority**: This is the core reason the app exists — an always-available, zero-effort view of "how close am I to my limit and when does it reset". Delivered alone it is already a complete, useful product.

**Independent Test**: Launch the app so its icon appears in the notification area, then hover over / click the icon; confirm the current usage (amount consumed vs. limit) and the reset time/countdown are shown, and that they match the user's actual Claude usage.

**Acceptance Scenarios**:

1. **Given** the app is running and usage data is available, **When** the user hovers over the tray icon, **Then** a summary shows the current usage of the active limit and the time remaining until it resets.
2. **Given** the app is running, **When** the user clicks the tray icon, **Then** a compact panel opens showing current usage, the limit total, and the reset time — without opening a full desktop window.
3. **Given** usage has changed since the last view, **When** the user next looks at the icon or panel, **Then** the displayed values reflect the latest available usage.
4. **Given** the current limit has been fully consumed, **When** the user views the summary, **Then** it clearly indicates the limit is reached and when it will reset.

---

### User Story 2 - See detailed usage statistics (Priority: P2)

A Claude user who wants more than the headline number opens a details view from the tray to understand their usage over time — for example how much they have used across recent periods (tokens/messages/cost or equivalent), broken down so they can spot trends and plan their work.

**Why this priority**: Adds depth for engaged users but is explicitly "if possible" in the request; the app is valuable without it. It depends on richer data being available beyond the current-limit snapshot.

**Independent Test**: Open the details view from the tray and confirm it presents usage statistics over a period when that data is available, and clearly indicates when such data is not available.

**Acceptance Scenarios**:

1. **Given** detailed usage data is available, **When** the user opens the details view, **Then** per-day usage statistics over the last ~30 days (tokens, message counts, estimated cost) are displayed as totals and a simple trend.
2. **Given** detailed usage data is not available, **When** the user opens the details view, **Then** the view clearly states that statistics are unavailable rather than showing blank or misleading values.

---

### User Story 3 - Be warned before hitting the limit (Priority: P3)

A Claude user who does not want to be surprised by a hit limit is given a clear visual signal (and optionally a notification) as usage approaches the limit, so they can adjust before being blocked.

**Why this priority**: A convenience that increases the value of the glanceable view but is not required for the core promise; builds on the P1 usage data.

**Independent Test**: Drive usage toward the limit (or simulate a near-limit state) and confirm the tray icon changes state to signal the approaching limit, and returns to normal after the limit resets.

**Acceptance Scenarios**:

1. **Given** the most-constraining window is below 80% consumed, **When** the user views the tray icon, **Then** it appears in its normal state.
2. **Given** the most-constraining window reaches ≥80% consumed, **When** the state next refreshes, **Then** the tray icon changes to a distinct warning state (and to a "reached" state at 100%).
3. **Given** the relevant window resets below 80%, **When** the state next refreshes, **Then** the tray icon returns to its normal state.

---

### Edge Cases

- **Data source unavailable / Claude not installed**: A required source is missing — no local Claude credentials/token (so the authenticated usage endpoint cannot be reached), the endpoint is unreachable, or the local statistics files are absent. Each source can fail independently; the app must show a clear "usage data unavailable" state with a plausible reason per source (e.g., "sign in to Claude required", "statistics history not found"), not a crash or a misleading zero, and must still show whichever data it can.
- **Stale data**: The last successful reading is older than a refresh interval (e.g., the source could not be read). The app must indicate the data is stale and show when it was last updated.
- **No usage yet in the current window**: The current limit window has zero consumption. The app must show 0% / full remaining and the correct reset time.
- **Multiple concurrent limits**: More than one limit window applies (e.g., a short rolling window and a longer weekly window). The app must make clear which figure is being shown and let the user see each.
- **Limit already reached**: Usage is at 100%. The app must clearly communicate "limit reached" and the reset time.
- **Reset boundary crossed**: The reset time passes while the app is running. The displayed usage and countdown must roll over to the new window without requiring a restart.
- **System resume / notification area unavailable**: The machine wakes from sleep, or the notification area is temporarily hidden/overflowed. The app must recover its icon and refresh its data.
- **Clock / time-zone differences**: The reset time must be shown correctly relative to the user's local time.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST place and maintain a single persistent icon in the Windows notification area (system tray) while running.
- **FR-002**: System MUST track both applicable limit windows — a short rolling window (~5 hours) and a weekly window — and, for the tray headline/summary, MUST display the usage of the **most-constraining** window (the one closest to its limit), expressed as the amount consumed relative to the total (e.g., a percentage and/or consumed-of-total figure).
- **FR-003**: System MUST display when the headlined limit resets, as a countdown and/or a local timestamp; the compact panel MUST list each window (rolling and weekly) with its own usage and reset.
- **FR-004**: Users MUST be able to see the current usage and reset information without opening a full application window — at minimum via a tooltip on hover and a compact panel on interaction with the tray icon.
- **FR-005**: System MUST automatically refresh the displayed usage on a regular interval and reflect changes without requiring the user to restart the app.
- **FR-006**: Users MUST be able to trigger an on-demand refresh.
- **FR-007**: System MUST present usage statistics as a per-day trend over the last ~30 days — tokens, message counts, and estimated cost, shown as totals and a simple trend — when local data is available, and MUST clearly indicate when it is not available (the statistics view is best-effort/optional value). Per-model and per-project breakdowns are out of scope for v1.
- **FR-008**: System MUST clearly communicate when usage data is unavailable or stale, including the time of the last successful reading and, where possible, the reason.
- **FR-009**: System MUST distinguish between multiple limit windows when more than one applies, making clear which limit each displayed value refers to.
- **FR-010**: System MUST provide a way to exit the application from the tray icon.
- **FR-011**: System MUST change the tray icon to a warning state when the most-constraining window reaches ≥80% consumed, to a distinct "reached" state at 100%, and MUST restore the normal state after the relevant window resets below the threshold.
- **FR-012**: Users SHOULD be able to configure whether the app launches automatically when Windows starts.
- **FR-013**: System MUST be read-only with respect to Claude usage data — it MUST never modify, reset, or otherwise alter the user's usage, limits, or the underlying data source.
- **FR-014**: System MUST run continuously in the background as a lightweight resident application with no primary window required for normal use.
- **FR-015**: System MUST obtain current usage and limit-reset values from an authenticated Anthropic usage endpoint, reusing the Claude credentials/OAuth token already present on the machine, without asking the user to enter or manage keys.
- **FR-016**: System MUST source historical usage statistics from local Claude data files, and MUST function for its core current-usage/reset view (P1) even when local statistics data is absent, and vice versa (each data source can be unavailable independently).

### Key Entities *(include if feature involves data)*

- **Usage Snapshot**: A point-in-time reading of the active limit — amount consumed, limit total, derived percentage, which limit window it refers to, the reset time for that window, and the timestamp the reading was captured.
- **Limit Window**: A period over which a limit applies. Two kinds are tracked: a short **rolling window** (~5 hours) and a **weekly window**. Each has its own consumed amount, total, derived percentage, and reset time. The window with the highest percentage consumed is the "most-constraining" one used for the tray headline.
- **Usage Statistics**: Per-day aggregates over the last ~30 days — tokens consumed, message count, and estimated cost per day — plus roll-up totals for the period, used to show recent activity and a simple trend. Sourced from local Claude data files; shown best-effort when available.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can determine their current usage and time-to-reset within 3 seconds of interacting with the tray icon, without opening a separate application window.
- **SC-002**: Displayed usage reflects the latest available data within 60 seconds of it changing at the source (auto-refresh), and immediately after a manual refresh.
- **SC-003**: The displayed usage and reset time match the user's actual Claude usage (the recognized source of truth) exactly, or within a clearly documented tolerance.
- **SC-004**: When usage data is unavailable or stale, the user sees a clear status and (where possible) reason 100% of the time — the app never shows a blank, silent, or misleading value in these states.
- **SC-005**: At least 90% of first-time users can locate the current usage and reset time on their first interaction with the tray icon, without instructions.
- **SC-006**: The app remains resident in the background with negligible perceived impact on system performance (near-zero CPU when idle and modest memory footprint), so users leave it running all day.
- **SC-007**: After a reset boundary passes, the displayed usage and countdown roll over to the new window automatically within one refresh interval, with no restart required.

## Assumptions

- The target user runs Claude on the same Windows machine and has produced usage data that the app can read; the app is a companion viewer, not a Claude client itself.
- The app uses a **hybrid** data source (resolved in Clarifications): the source of truth for current usage and limit reset is an authenticated Anthropic usage endpoint reached by reusing the local Claude credentials/OAuth token already stored on the machine (no separate key entry by the user), and richer historical statistics are read from local Claude data files. The app depends on the availability of both, and degrades gracefully when either is missing (see Edge Cases and FR-008).
- "Current usage" means the proportion of the active rate-limit window that has been consumed; "limit reset" means when that window resets; "usage stats" means aggregate usage over time, provided on a best-effort basis when the source exposes it.
- A single active Claude account/plan is assumed for v1; switching between multiple accounts is out of scope.
- The environment is Windows 10/11 with a standard, visible notification area.
- Auto-refresh runs on a periodic interval (default roughly once per minute), in addition to manual refresh; the exact interval is tunable and not a hard user-facing contract beyond SC-002.
- Presentation follows platform norms (theme/localization) and is not a v1 differentiator.
- The app is a background utility with no full main window required for its core value; any details/statistics surface is a secondary view opened on demand.
