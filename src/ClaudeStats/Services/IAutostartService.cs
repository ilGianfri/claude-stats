namespace ClaudeStats.Services;

/// <summary>
/// Enables, disables, and queries launch-at-Windows-logon for the current user.
/// </summary>
public interface IAutostartService
{
    /// <summary>Determines whether launch-at-startup is currently enabled.</summary>
    /// <returns><see langword="true"/> if enabled.</returns>
    bool IsEnabled();

    /// <summary>Enables or disables launch-at-startup.</summary>
    /// <param name="enabled">Whether to enable autostart.</param>
    void SetEnabled(bool enabled);
}
