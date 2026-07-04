namespace ClaudeStats.Services;

/// <summary>
/// Allows an on-demand usage refresh outside the normal polling cadence.
/// </summary>
public interface IUsagePoller
{
    /// <summary>Performs a single usage poll immediately and broadcasts the result.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the poll has finished.</returns>
    Task RefreshNowAsync(CancellationToken ct = default);
}
