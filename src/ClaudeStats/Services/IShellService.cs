namespace ClaudeStats.Services;

/// <summary>
/// Application-level UI actions that require the WPF shell, exposed as an abstraction so
/// ViewModels stay free of <c>System.Windows</c> (Constitution IV).
/// </summary>
public interface IShellService
{
    /// <summary>Exits the application.</summary>
    void Exit();

    /// <summary>Opens the usage-statistics (trend) window.</summary>
    void ShowTrend();

    /// <summary>Opens the settings window.</summary>
    void ShowSettings();

    /// <summary>Shows a transient tray notification.</summary>
    /// <param name="title">Notification title.</param>
    /// <param name="message">Notification body.</param>
    void Notify(string title, string message);
}
