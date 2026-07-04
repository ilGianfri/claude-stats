using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Windows;
using ClaudeStats.Configuration;
using ClaudeStats.Services;
using ClaudeStats.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;

namespace ClaudeStats;

/// <summary>
/// Application entry point. Bootstraps the .NET Generic Host (DI, logging, configuration),
/// enforces a single running instance, and hosts the tray shell with no main window.
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private Mutex? _singleInstanceMutex;
    private TaskbarIcon? _trayIcon;

    /// <summary>Gets the host's service provider once the host has started.</summary>
    public IServiceProvider Services =>
        _host?.Services ?? throw new InvalidOperationException("Host is not started.");

    /// <summary>Builds and starts the host, or exits if another instance already owns the mutex.</summary>
    /// <param name="e">Startup event arguments.</param>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, @"Local\ClaudeStats.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            // Another instance is already running.
            Shutdown();
            return;
        }

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        ConfigureServices(builder.Services);
        _host = builder.Build();
        await _host.StartAsync();

        CreateTrayIcon();

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    /// <summary>Forces a usage refresh when the machine resumes from sleep.</summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Power-mode change details.</param>
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume && _host is not null)
        {
            IUsagePoller poller = Services.GetRequiredService<IUsagePoller>();
            _ = poller.RefreshNowAsync();
        }
    }

    /// <summary>Registers all application services in the dependency-injection container.</summary>
    /// <param name="services">The service collection to populate.</param>
    private static void ConfigureServices(IServiceCollection services)
    {
        // Foundational (Phase 2)
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IUiDispatcher, WpfDispatcher>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<AppSettings>(sp => sp.GetRequiredService<ISettingsStore>().Load());
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.AddSingleton<IShellService, ShellService>();

        // User Story 1 — current usage & reset
        services.AddSingleton<ICredentialStore, CredentialStore>();

        services.AddHttpClient<IOAuthTokenService, OAuthTokenService>(ConfigureOAuthClient);
        services.AddHttpClient<IUsageClient, UsageClient>(ConfigureUsageClient);

        services.AddSingleton<PollingService>();
        services.AddSingleton<IUsagePoller>(sp => sp.GetRequiredService<PollingService>());
        services.AddHostedService(sp => sp.GetRequiredService<PollingService>());

        services.AddSingleton<TrayViewModel>();

        // User Story 2 — usage statistics
        services.AddSingleton<IPricingProvider, PricingProvider>();
        services.AddSingleton<IUsageStatsReader, TranscriptUsageStatsReader>();
        services.AddTransient<TrendViewModel>();

        // Polish — settings / autostart
        services.AddSingleton<IAutostartService, RegistryAutostartService>();
        services.AddTransient<SettingsViewModel>();
    }

    /// <summary>Configures the typed HTTP client for the usage endpoint.</summary>
    /// <param name="client">The client to configure.</param>
    private static void ConfigureUsageClient(HttpClient client)
    {
        client.BaseAddress = new Uri(ClaudeApi.ApiBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(ClaudeApi.UserAgent);
        client.DefaultRequestHeaders.Add("anthropic-beta", ClaudeApi.AnthropicBeta);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>Configures the typed HTTP client for the OAuth token endpoint.</summary>
    /// <param name="client">The client to configure.</param>
    private static void ConfigureOAuthClient(HttpClient client)
    {
        client.BaseAddress = new Uri(ClaudeApi.PlatformBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(ClaudeApi.UserAgent);
        client.DefaultRequestHeaders.Add("anthropic-beta", ClaudeApi.AnthropicBeta);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>Creates the tray icon, binds it to its view model, and shows it.</summary>
    private void CreateTrayIcon()
    {
        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        _trayIcon.DataContext = Services.GetRequiredService<TrayViewModel>();
        _trayIcon.ForceCreate();
    }

    /// <summary>Stops the host, disposes the tray icon, and releases the mutex on shutdown.</summary>
    /// <param name="e">Exit event arguments.</param>
    protected override async void OnExit(ExitEventArgs e)
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _trayIcon?.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(2));
            _host.Dispose();
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
