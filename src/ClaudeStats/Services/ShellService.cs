using System.Windows;
using ClaudeStats.ViewModels;
using ClaudeStats.Views;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeStats.Services;

/// <summary>
/// WPF implementation of <see cref="IShellService"/>. Owns app-level window/lifecycle actions so
/// ViewModels remain UI-agnostic. Windows are single-instance and reactivated if already open.
/// </summary>
public sealed class ShellService : IShellService
{
    private readonly IServiceProvider _services;
    private TrendWindow? _trendWindow;
    private SettingsView? _settingsWindow;

    /// <summary>Initializes the shell service.</summary>
    /// <param name="services">Service provider used to resolve windows/ViewModels on demand.</param>
    public ShellService(IServiceProvider services)
    {
        _services = services;
    }

    /// <inheritdoc />
    public void Exit()
    {
        Application.Current?.Shutdown();
    }

    /// <inheritdoc />
    public void ShowTrend()
    {
        if (_trendWindow is not null)
        {
            _trendWindow.Activate();
            return;
        }

        TrendViewModel viewModel = _services.GetRequiredService<TrendViewModel>();
        _trendWindow = new TrendWindow { DataContext = viewModel };
        _trendWindow.Closed += (_, _) => _trendWindow = null;
        _trendWindow.Show();
        viewModel.LoadCommand.Execute(null);
    }

    /// <inheritdoc />
    public void ShowSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        SettingsViewModel viewModel = _services.GetRequiredService<SettingsViewModel>();
        _settingsWindow = new SettingsView { DataContext = viewModel };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    /// <inheritdoc />
    public void Notify(string title, string message)
    {
        if (Application.Current?.TryFindResource("TrayIcon") is TaskbarIcon icon)
        {
            icon.ShowNotification(title, message);
        }
    }
}

