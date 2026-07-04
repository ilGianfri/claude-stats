using ClaudeStats.Services;
using Microsoft.Win32;
using Xunit;

namespace ClaudeStats.Tests.Services;

/// <summary>
/// Unit tests for <see cref="RegistryAutostartService"/>. Uses a throwaway HKCU Run value name that
/// is always removed on dispose.
/// </summary>
public sealed class RegistryAutostartServiceTests : IDisposable
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string _valueName = $"ClaudeStats_Test_{Guid.NewGuid():N}";
    private readonly RegistryAutostartService _service;

    /// <summary>Creates the service with a unique test value name and a dummy exe path.</summary>
    public RegistryAutostartServiceTests()
    {
        _service = new RegistryAutostartService(_valueName, @"C:\Test\ClaudeStats.exe");
    }

    [Fact]
    public void SetEnabled_TogglesRegistryValue()
    {
        Assert.False(_service.IsEnabled());

        _service.SetEnabled(true);
        Assert.True(_service.IsEnabled());

        _service.SetEnabled(false);
        Assert.False(_service.IsEnabled());
    }

    /// <summary>Removes the test registry value.</summary>
    public void Dispose()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(_valueName, throwOnMissingValue: false);
    }
}
