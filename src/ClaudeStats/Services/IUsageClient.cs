using ClaudeStats.Models;

namespace ClaudeStats.Services;

/// <summary>
/// Fetches the current subscription usage/limit snapshot from the authenticated endpoint.
/// </summary>
public interface IUsageClient
{
    /// <summary>Fetches the current usage snapshot, refreshing the token on a 401 if needed.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The current usage snapshot.</returns>
    /// <exception cref="UsageUnavailableException">Thrown when a reading cannot be produced.</exception>
    Task<UsageSnapshot> GetUsageAsync(CancellationToken ct);
}
