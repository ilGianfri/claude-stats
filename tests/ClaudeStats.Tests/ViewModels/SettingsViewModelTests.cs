using ClaudeStats.Configuration;
using ClaudeStats.Services;
using ClaudeStats.ViewModels;
using NSubstitute;
using Xunit;

namespace ClaudeStats.Tests.ViewModels;

/// <summary>Unit tests for <see cref="SettingsViewModel"/>.</summary>
public sealed class SettingsViewModelTests
{
    private readonly IAutostartService _autostart = Substitute.For<IAutostartService>();
    private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();
    private readonly AppSettings _settings = new();

    [Fact]
    public void Constructor_ReflectsCurrentAutostartState()
    {
        _autostart.IsEnabled().Returns(true);
        SettingsViewModel vm = new(_autostart, _store, _settings);
        Assert.True(vm.LaunchAtStartup);
    }

    [Fact]
    public void EnablingLaunchAtStartup_AppliesAndPersists()
    {
        _autostart.IsEnabled().Returns(false);
        SettingsViewModel vm = new(_autostart, _store, _settings);

        vm.LaunchAtStartup = true;

        _autostart.Received(1).SetEnabled(true);
        _store.Received(1).Save(Arg.Is<AppSettings>(s => s.LaunchAtStartup));
        Assert.True(_settings.LaunchAtStartup);
    }
}
