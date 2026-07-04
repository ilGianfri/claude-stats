using System.IO;
using System.Text.Json;
using ClaudeStats.Configuration;
using ClaudeStats.Json;
using Microsoft.Extensions.Logging;

namespace ClaudeStats.Services;

/// <summary>
/// Persists <see cref="AppSettings"/> as JSON under <c>%APPDATA%\ClaudeStats\settings.json</c>.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _filePath;
    private readonly ILogger<JsonSettingsStore> _logger;

    /// <summary>Initializes the store, resolving the settings file path under %APPDATA%.</summary>
    /// <param name="logger">Logger for load/save diagnostics.</param>
    public JsonSettingsStore(ILogger<JsonSettingsStore> logger)
    {
        _logger = logger;
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClaudeStats");
        _filePath = Path.Combine(dir, "settings.json");
    }

    /// <inheritdoc />
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new AppSettings();
            }

            string json = File.ReadAllText(_filePath);
            AppSettings? settings = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettings);
            return settings ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed to load settings; using defaults.");
            return new AppSettings();
        }
    }

    /// <inheritdoc />
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            string? dir = Path.GetDirectoryName(_filePath);
            if (dir is not null)
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(settings, AppJsonContext.Default.AppSettings);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed to save settings.");
        }
    }
}
