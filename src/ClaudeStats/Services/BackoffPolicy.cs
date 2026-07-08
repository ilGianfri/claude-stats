namespace ClaudeStats.Services;

/// <summary>
/// Computes the delay before the next usage poll. While transient failures (e.g. HTTP 429) persist
/// it grows the delay exponentially from the base interval up to a ceiling; a successful poll resets
/// it back to the base interval. A server-supplied <c>Retry-After</c> hint takes precedence when it
/// asks for a longer wait than the computed back-off.
/// </summary>
public sealed class BackoffPolicy
{
    private readonly TimeSpan _baseInterval;
    private readonly TimeSpan _maxInterval;
    private int _consecutiveFailures;

    /// <summary>Initializes the policy.</summary>
    /// <param name="baseInterval">The normal poll interval, applied after a successful poll.</param>
    /// <param name="maxInterval">The maximum computed back-off delay (floored at <paramref name="baseInterval"/>).</param>
    public BackoffPolicy(TimeSpan baseInterval, TimeSpan maxInterval)
    {
        _baseInterval = baseInterval;
        _maxInterval = maxInterval < baseInterval ? baseInterval : maxInterval;
    }

    /// <summary>Gets the normal poll interval applied after a successful poll.</summary>
    public TimeSpan BaseInterval => _baseInterval;

    /// <summary>Clears the failure streak so the next delay returns to the base interval.</summary>
    public void Reset() => _consecutiveFailures = 0;

    /// <summary>Records a transient failure and returns how long to wait before the next attempt.</summary>
    /// <param name="retryAfter">Optional server-provided wait hint (e.g. from a Retry-After header).</param>
    /// <returns>Exponential back-off clamped to [base, max], overridden upward by <paramref name="retryAfter"/> when it is longer.</returns>
    public TimeSpan NextDelay(TimeSpan? retryAfter = null)
    {
        _consecutiveFailures++;

        // Exponential: base * 2^(failures-1). Cap the exponent so the multiplication cannot overflow.
        int exponent = Math.Min(_consecutiveFailures - 1, 16);
        TimeSpan delay = _baseInterval * Math.Pow(2, exponent);

        if (delay > _maxInterval)
        {
            delay = _maxInterval;
        }

        // Honor an explicit server hint when it wants us to wait longer than our own back-off.
        if (retryAfter is { } hint && hint > delay)
        {
            delay = hint;
        }

        return delay < _baseInterval ? _baseInterval : delay;
    }
}
