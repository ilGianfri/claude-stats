<!--
Sync Impact Report
==================
Version change: (unversioned template) → 1.0.0
Bump rationale: Initial ratification. Template placeholders replaced with concrete
  principles for the ClaudeStats WPF / .NET 10 / CommunityToolkit.Mvvm desktop app.

Principles defined (first definition — no prior versions):
  [PRINCIPLE_1] → I. MVVM Separation (NON-NEGOTIABLE)
  [PRINCIPLE_2] → II. CommunityToolkit.Mvvm First
  [PRINCIPLE_3] → III. Data Binding Over Code-Behind
  [PRINCIPLE_4] → IV. Testable, UI-Agnostic ViewModels
  [PRINCIPLE_5] → V. Responsive Async UI

Sections defined:
  [SECTION_2_NAME] → Technology & Platform Constraints
  [SECTION_3_NAME] → Development Workflow & Quality Gates

Removed sections: none

Templates reviewed for consistency:
  ✅ .specify/templates/plan-template.md — "Constitution Check" is filled at plan
       time from this file; MVVM gates map cleanly. No change required.
  ✅ .specify/templates/spec-template.md — no mandatory sections added/removed. No change required.
  ✅ .specify/templates/tasks-template.md — phase/story task model is compatible. No change required.
  ✅ .specify/templates/checklist-template.md — generic; unaffected.

Follow-up TODOs: none. RATIFICATION_DATE set to today (new project, first adoption).
-->

# ClaudeStats Constitution

## Core Principles

### I. MVVM Separation (NON-NEGOTIABLE)

Every screen follows the Model-View-ViewModel pattern with strict layer boundaries:

- Views (XAML) MUST contain only presentation markup and view-specific behavior; they MUST
  NOT contain business logic or data-access code.
- ViewModels MUST expose all UI state as bindable properties and all user actions as commands;
  they MUST NOT reference WPF element types (`Window`, `Button`, `System.Windows.Controls.*`).
- Models MUST represent domain data and rules only, with no knowledge of Views or ViewModels.

**Rationale**: Separation keeps the UI thin, the logic testable, and the domain reusable.

### II. CommunityToolkit.Mvvm First

Change notification, commands, and in-app messaging MUST be implemented with
CommunityToolkit.Mvvm rather than hand-written plumbing:

- ViewModels MUST derive from `ObservableObject` (or `ObservableRecipient` when messaging
  is required).
- Bindable properties MUST use the `[ObservableProperty]` source generator; commands MUST use
  `[RelayCommand]`.
- Cross-component communication SHOULD use `IMessenger` instead of direct references or static
  events.
- Hand-rolled `INotifyPropertyChanged` / `ICommand` boilerplate and additional MVVM frameworks
  are prohibited unless a documented toolkit gap is justified in the plan's Complexity Tracking.

**Rationale**: One idiomatic, source-generated approach removes boilerplate and keeps the
codebase consistent.

### III. Data Binding Over Code-Behind

UI and state MUST be connected through data binding, not imperative control manipulation:

- User interactions MUST be wired via `Command`/binding, not code-behind event handlers, except
  for genuinely view-only concerns (animation, focus, drag visuals).
- Code-behind (`*.xaml.cs`) MUST NOT read or mutate ViewModel state directly; it defaults to
  `InitializeComponent()` only.
- The `DataContext` MUST be a ViewModel; Views MUST NOT instantiate services or perform I/O.

**Rationale**: Bindings make the UI declarative, testable, and designer-friendly.

### IV. Testable, UI-Agnostic ViewModels

ViewModels and Models MUST be unit-testable without a running UI:

- External dependencies (file system, HTTP, Claude data sources, dialogs, navigation) MUST be
  accessed through interfaces injected via the constructor.
- No ViewModel or Model may depend on `System.Windows.*` or the `Dispatcher` directly; UI-thread
  marshaling MUST be provided through an abstraction.
- Every ViewModel with non-trivial logic MUST have unit tests covering its commands and state
  transitions.

**Rationale**: Dependency inversion enables fast, deterministic tests and decouples logic from
the WPF shell.

### V. Responsive Async UI

The UI thread MUST remain responsive at all times:

- I/O-bound and CPU-bound work MUST run off the UI thread using `async`/`await` over
  `Task`-returning APIs.
- Async operations MUST use `[RelayCommand]` async support and expose `CanExecute`/progress state;
  the `Dispatcher` MUST NOT be blocked (`.Result`, `.Wait()`, and `Thread.Sleep` on the UI thread
  are prohibited).
- Long-running or user-initiated operations SHOULD honor a `CancellationToken`.

**Rationale**: A WPF app that blocks the dispatcher is perceived as broken; async keeps it fluid.

## Technology & Platform Constraints

- Target framework MUST be `net10.0-windows` (.NET 10); the project MUST build with `UseWPF=true`.
- `Nullable` and `ImplicitUsings` MUST remain enabled; new code MUST be null-annotation clean and
  introduce no new nullable warnings.
- C# code MUST use explicit types instead of `var`.
- The presentation stack is WPF + CommunityToolkit.Mvvm; introducing an alternative UI or MVVM
  framework requires documented justification in Complexity Tracking.
- Shared internal functionality is delivered through `alebro.<servicename>` libraries where
  applicable; their source is maintained in-house rather than consumed as third-party packages.

## Development Workflow & Quality Gates

- Every new method MUST carry an XML documentation comment: a concise one-line `<summary>`,
  a `<param>` tag for each parameter, and `<returns>` when the method returns a value.
- Changes MUST compile cleanly (no new warnings) before review; ViewModel logic changes MUST add
  or update unit tests.
- Code review MUST verify conformance to the Core Principles; each violation requires an entry in
  the plan's Complexity Tracking table with justification, or it blocks merge.
- Feature work follows the Spec Kit flow (specify → plan → tasks → implement); the Constitution
  Check gate in the plan MUST pass before implementation begins.

## Governance

- This constitution supersedes ad-hoc conventions for the ClaudeStats project. Where guidance
  conflicts, the constitution wins.
- Amendments MUST be proposed as a documented change (rationale + affected principles), reviewed,
  and recorded here with a version bump and an updated amendment date.
- Versioning follows semantic versioning: MAJOR for backward-incompatible governance/principle
  removals or redefinitions, MINOR for a newly added principle or materially expanded section,
  PATCH for clarifications and wording fixes.
- Compliance is reviewed at every code review and at each `/speckit-plan` Constitution Check;
  unjustified violations block merge.

**Version**: 1.0.0 | **Ratified**: 2026-07-04 | **Last Amended**: 2026-07-04
