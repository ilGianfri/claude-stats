# Contract (internal): Service Interfaces

The app's own seams for DI + testability (Constitution IV). All async methods take a
`CancellationToken`. Signatures are indicative; implementations get full XML doc comments.

```csharp
/// <summary>Reads (and refreshes) the local Claude OAuth credentials.</summary>
public interface ICredentialStore
{
    Task<OAuthCredentials?> ReadAsync(CancellationToken ct);
    Task WriteAsync(OAuthCredentials updated, CancellationToken ct); // preserves other keys, atomic
}

/// <summary>Refreshes an expired OAuth access token.</summary>
public interface IOAuthTokenService
{
    Task<OAuthCredentials> RefreshAsync(OAuthCredentials current, CancellationToken ct);
}

/// <summary>Fetches the current subscription usage/limit snapshot.</summary>
public interface IUsageClient
{
    Task<UsageSnapshot> GetUsageAsync(CancellationToken ct); // 401 → refresh once → retry
}

/// <summary>Aggregates local transcripts into a ~30-day statistics roll-up.</summary>
public interface IUsageStatsReader
{
    Task<UsageStatistics> GetStatisticsAsync(int days, CancellationToken ct);
}

/// <summary>Provides per-model token pricing for cost estimation.</summary>
public interface IPricingProvider
{
    ModelPrice? TryGet(string modelId);
}

/// <summary>Enables/queries launch-at-Windows-startup (HKCU Run key).</summary>
public interface IAutostartService
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}

/// <summary>Marshals work onto the UI thread (keeps System.Windows out of ViewModels).</summary>
public interface IUiDispatcher
{
    Task InvokeAsync(Action action);
}

/// <summary>Abstracts the current time for testable countdowns/day-bucketing.</summary>
public interface IClock
{
    DateTimeOffset Now { get; }
}
```

## Messaging (CommunityToolkit.Mvvm `IMessenger`)
- `UsageUpdatedMessage(UsageSnapshot Snapshot)` — broadcast by `PollingService` after each successful
  read; `TrayViewModel` updates headline + icon state.
- `UsageErrorMessage(LimitState State, string Reason)` — broadcast on failure; ViewModels reflect
  `Unavailable`/`Stale`.

## Consumers
- `PollingService` → `IUsageClient` (+ `IUiDispatcher`, `IMessenger`, `AppSettings.RefreshInterval`).
- `IUsageClient` → `ICredentialStore`, `IOAuthTokenService`, `IHttpClientFactory`.
- `TrendViewModel` → `IUsageStatsReader` (async on view open).
- `SettingsViewModel` → `IAutostartService`, `AppSettings`.
- All time-dependent logic → `IClock`.
