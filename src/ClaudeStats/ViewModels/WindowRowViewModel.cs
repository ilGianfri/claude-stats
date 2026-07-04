namespace ClaudeStats.ViewModels;

/// <summary>
/// Read-only row describing a single limit window for the tray popup.
/// </summary>
/// <param name="Name">Window display name (e.g. "5-hour").</param>
/// <param name="Percent">Consumed percentage (0–100).</param>
/// <param name="PercentText">Formatted percentage (e.g. "42%").</param>
/// <param name="ResetText">Formatted reset countdown (e.g. "resets in 2h 13m").</param>
public sealed record WindowRowViewModel(string Name, double Percent, string PercentText, string ResetText);
