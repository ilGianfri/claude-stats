using System.Collections.ObjectModel;
using ClaudeStats.Configuration;
using ClaudeStats.Messages;
using ClaudeStats.Models;
using ClaudeStats.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace ClaudeStats.ViewModels;

/// <summary>
/// Drives the tray icon: headline usage, reset countdown, per-window list, status/tooltip text,
/// and the refresh/exit/trend/settings commands. Receives usage updates via the messenger.
/// </summary>
public sealed partial class TrayViewModel : ObservableRecipient,
    IRecipient<UsageUpdatedMessage>,
    IRecipient<UsageErrorMessage>
{
    private const string NormalIcon = "pack://application:,,,/Resources/icons/normal.ico";
    private const string WarningIcon = "pack://application:,,,/Resources/icons/warning.ico";
    private const string ReachedIcon = "pack://application:,,,/Resources/icons/reached.ico";

    private readonly IUsagePoller _poller;
    private readonly IShellService _shell;
    private readonly IClock _clock;
    private readonly double _warningThresholdPercent;
    private int _lastNotifiedSeverity;

    /// <summary>Current presentation state of the most-constraining window.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconResourcePath))]
    private LimitState _state = LimitState.Unavailable;

    /// <summary>Headline text shown as the primary figure (e.g. "42%").</summary>
    [ObservableProperty]
    private string _headlineText = "…";

    /// <summary>Reset countdown text for the headline window.</summary>
    [ObservableProperty]
    private string _resetText = string.Empty;

    /// <summary>Tooltip / status text.</summary>
    [ObservableProperty]
    private string _statusText = "ClaudeStats — starting…";

    /// <summary>When the displayed data was last refreshed.</summary>
    [ObservableProperty]
    private string _lastUpdatedText = string.Empty;

    /// <summary>Per-window rows for the popup panel.</summary>
    public ObservableCollection<WindowRowViewModel> Windows { get; } = [];

    /// <summary>Pack URI of the tray icon appropriate to the current <see cref="State"/>.</summary>
    public string IconResourcePath => State switch
    {
        LimitState.Warning => WarningIcon,
        LimitState.Reached => ReachedIcon,
        _ => NormalIcon,
    };

    /// <summary>Initializes the tray view model and activates message reception.</summary>
    /// <param name="poller">Usage poller for on-demand refresh.</param>
    /// <param name="shell">Shell service for app-level UI actions.</param>
    /// <param name="clock">Clock for reset countdowns.</param>
    /// <param name="settings">Application settings (warning threshold).</param>
    /// <param name="messenger">Messenger for usage update/error messages.</param>
    public TrayViewModel(
        IUsagePoller poller,
        IShellService shell,
        IClock clock,
        AppSettings settings,
        IMessenger messenger)
        : base(messenger)
    {
        _poller = poller;
        _shell = shell;
        _clock = clock;
        _warningThresholdPercent = settings.WarningThresholdPercent;
        IsActive = true;
    }

    /// <inheritdoc />
    public void Receive(UsageUpdatedMessage message) => Apply(message.Snapshot);

    /// <inheritdoc />
    public void Receive(UsageErrorMessage message) => ApplyError(message.State, message.Reason);

    /// <summary>Triggers an immediate usage refresh.</summary>
    /// <returns>A task that completes when the refresh finishes.</returns>
    [RelayCommand]
    private Task RefreshAsync() => _poller.RefreshNowAsync();

    /// <summary>Exits the application.</summary>
    [RelayCommand]
    private void Exit() => _shell.Exit();

    /// <summary>Opens the usage-statistics window.</summary>
    [RelayCommand]
    private void ShowTrend() => _shell.ShowTrend();

    /// <summary>Opens the settings window.</summary>
    [RelayCommand]
    private void ShowSettings() => _shell.ShowSettings();

    /// <summary>Applies a fresh usage snapshot to the bound state.</summary>
    /// <param name="snapshot">The snapshot to display.</param>
    private void Apply(UsageSnapshot snapshot)
    {
        LimitWindow? headline = snapshot.MostConstraining;
        if (headline is null)
        {
            ApplyError(LimitState.Unavailable, "No usage data returned.");
            return;
        }

        State = snapshot.GetState(_warningThresholdPercent);
        NotifyOnEscalation(State);
        DateTimeOffset now = _clock.Now;
        HeadlineText = FormatPercent(headline.Percent);
        ResetText = FormatReset(headline.GetTimeRemaining(now));
        StatusText = $"{headline.DisplayName}: {FormatPercent(headline.Percent)} · {ResetText}";
        LastUpdatedText = $"Updated {snapshot.CapturedAt.LocalDateTime:t}";

        Windows.Clear();
        foreach (LimitWindow window in snapshot.Windows)
        {
            Windows.Add(new WindowRowViewModel(
                window.DisplayName,
                window.Percent,
                FormatPercent(window.Percent),
                FormatReset(window.GetTimeRemaining(now))));
        }
    }

    /// <summary>Applies an error/unavailable/stale state.</summary>
    /// <param name="state">The error state.</param>
    /// <param name="reason">User-facing reason.</param>
    private void ApplyError(LimitState state, string reason)
    {
        State = state;
        if (state == LimitState.Unavailable)
        {
            HeadlineText = "—";
            ResetText = string.Empty;
            LastUpdatedText = string.Empty;
            Windows.Clear();
            StatusText = reason;
        }
        else
        {
            // Stale: keep the last known values, annotate the status.
            StatusText = Windows.Count > 0 ? $"{reason} — showing last known" : reason;
        }
    }

    /// <summary>Shows a one-time notification when usage escalates into a warning/reached state.</summary>
    /// <param name="state">The newly derived state.</param>
    private void NotifyOnEscalation(LimitState state)
    {
        int severity = state switch
        {
            LimitState.Warning => 1,
            LimitState.Reached => 2,
            _ => 0,
        };

        if (severity > _lastNotifiedSeverity)
        {
            string message = state == LimitState.Reached
                ? "You've reached your Claude usage limit."
                : "You've used 80% of your Claude usage limit.";
            _shell.Notify("Claude usage", message);
        }

        _lastNotifiedSeverity = severity;
    }

    /// <summary>Formats a percentage for display.</summary>
    /// <param name="percent">Percentage 0–100.</param>
    /// <returns>The formatted string (e.g. "42%").</returns>
    private static string FormatPercent(double percent) =>
        $"{Math.Round(percent, MidpointRounding.AwayFromZero):0}%";

    /// <summary>Formats a reset countdown for display.</summary>
    /// <param name="remaining">Time until reset.</param>
    /// <returns>A human-readable countdown (e.g. "resets in 2h 13m").</returns>
    private static string FormatReset(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
        {
            return "resetting…";
        }

        if (remaining.TotalDays >= 1)
        {
            return $"resets in {(int)remaining.TotalDays}d {remaining.Hours}h";
        }

        if (remaining.TotalHours >= 1)
        {
            return $"resets in {(int)remaining.TotalHours}h {remaining.Minutes}m";
        }

        return remaining.TotalMinutes >= 1 ? $"resets in {remaining.Minutes}m" : "resets in <1m";
    }
}
