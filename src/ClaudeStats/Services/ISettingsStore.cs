using ClaudeStats.Configuration;

namespace ClaudeStats.Services;

/// <summary>
/// Loads and persists <see cref="AppSettings"/>.
/// </summary>
public interface ISettingsStore
{
    /// <summary>Loads settings, returning defaults if none are stored or the file is unreadable.</summary>
    /// <returns>The current application settings.</returns>
    AppSettings Load();

    /// <summary>Persists the supplied settings.</summary>
    /// <param name="settings">The settings to save.</param>
    void Save(AppSettings settings);
}
