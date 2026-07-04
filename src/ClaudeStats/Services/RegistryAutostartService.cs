using Microsoft.Win32;

namespace ClaudeStats.Services;

/// <summary>
/// Implements launch-at-startup via the per-user HKCU <c>...\Run</c> registry key (no elevation).
/// </summary>
public sealed class RegistryAutostartService : IAutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string _valueName;
    private readonly string _executablePath;

    /// <summary>Initializes the service for the running executable.</summary>
    public RegistryAutostartService()
        : this("ClaudeStats", Environment.ProcessPath ?? string.Empty)
    {
    }

    /// <summary>Initializes the service with an explicit value name and executable path (used by tests).</summary>
    /// <param name="valueName">Registry value name under the Run key.</param>
    /// <param name="executablePath">Path to the executable to launch.</param>
    public RegistryAutostartService(string valueName, string executablePath)
    {
        _valueName = valueName;
        _executablePath = executablePath;
    }

    /// <inheritdoc />
    public bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(_valueName) is not null;
    }

    /// <inheritdoc />
    public void SetEnabled(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (enabled)
        {
            if (!string.IsNullOrEmpty(_executablePath))
            {
                key.SetValue(_valueName, $"\"{_executablePath}\"");
            }
        }
        else
        {
            key.DeleteValue(_valueName, throwOnMissingValue: false);
        }
    }
}
