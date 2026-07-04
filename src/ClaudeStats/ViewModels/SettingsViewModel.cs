using ClaudeStats.Configuration;
using ClaudeStats.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudeStats.ViewModels;

/// <summary>
/// Drives the settings window: launch-at-startup toggle (persisted immediately).
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IAutostartService _autostart;
    private readonly ISettingsStore _store;
    private readonly AppSettings _settings;

    /// <summary>Whether the app launches at Windows logon.</summary>
    [ObservableProperty]
    private bool _launchAtStartup;

    /// <summary>Initializes the settings view model from current state.</summary>
    /// <param name="autostart">Autostart service.</param>
    /// <param name="store">Settings store for persistence.</param>
    /// <param name="settings">Current application settings.</param>
    public SettingsViewModel(IAutostartService autostart, ISettingsStore store, AppSettings settings)
    {
        _autostart = autostart;
        _store = store;
        _settings = settings;
        _launchAtStartup = autostart.IsEnabled();
    }

    /// <summary>Applies and persists a change to the launch-at-startup preference.</summary>
    /// <param name="value">The new value.</param>
    partial void OnLaunchAtStartupChanged(bool value)
    {
        _autostart.SetEnabled(value);
        _settings.LaunchAtStartup = value;
        _store.Save(_settings);
    }
}
