namespace ClaudeStats.ViewModels;

/// <summary>
/// A single day's bar in the 30-day trend chart.
/// </summary>
/// <param name="DateLabel">Short date label (e.g. "Jul 4").</param>
/// <param name="TotalTokens">Total tokens that day.</param>
/// <param name="Fraction">Height fraction (0–1) relative to the busiest day.</param>
/// <param name="Tooltip">Detailed tooltip text.</param>
public sealed record TrendBarViewModel(string DateLabel, long TotalTokens, double Fraction, string Tooltip);
