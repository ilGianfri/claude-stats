# ClaudeStats

A lightweight Windows system-tray app that gives a quick look at your current Claude subscription
usage, when the limit resets, and a 30-day usage-statistics trend.

- **Glance (tray):** the tray icon shows the most-constraining limit window's usage %; hover for a
  tooltip and click for a popup listing each window (rolling ~5h and weekly) with its reset countdown.
- **Statistics:** a 30-day per-day trend of tokens, messages, and estimated cost.
- **Warnings:** the icon turns amber at ≥80% and red at 100% of the most-constraining window.
- **Autostart:** optionally launch at Windows logon.

## Requirements

- Windows 10/11
- .NET 10 SDK (build) / .NET 10 Desktop Runtime (run)
- Claude Code signed in on this machine (the app reuses the local OAuth token in
  `%USERPROFILE%\.claude\.credentials.json`; it never asks for or stores API keys, and only writes
  the credentials file to refresh an expired token). Historical statistics come from
  `%USERPROFILE%\.claude\projects\**\*.jsonl`.

## Build & run

```powershell
dotnet build ClaudeStats.slnx -c Release
dotnet run --project src/ClaudeStats/ClaudeStats.csproj
```

An icon appears in the notification area — there is no main window. Right-click it for
**Refresh**, **Usage stats…**, **Settings…**, and **Exit**.

## Test

```powershell
dotnet test ClaudeStats.slnx
```

## Architecture

WPF (.NET 10) + CommunityToolkit.Mvvm, hosted on the .NET Generic Host with constructor DI. See
[specs/001-claude-usage-tray/](specs/001-claude-usage-tray/) for the full spec, plan, data model, and
consumed-endpoint contracts. Note: the Claude usage endpoint is unofficial and may change; the app
isolates it behind `IUsageClient` and degrades gracefully when data is unavailable.
