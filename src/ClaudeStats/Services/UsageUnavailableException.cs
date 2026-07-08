namespace ClaudeStats.Services;

/// <summary>
/// Raised when a usage reading cannot be produced. Carries a user-facing reason and whether the
/// condition is transient (keep last snapshot, show as stale) or hard (show as unavailable).
/// </summary>
public sealed class UsageUnavailableException : Exception
{
    /// <summary>Initializes a new instance with a user-facing reason.</summary>
    /// <param name="reason">Short message suitable for display to the user.</param>
    /// <param name="isTransient">Whether the failure is transient (e.g. network/429).</param>
    /// <param name="innerException">Optional underlying exception.</param>
    /// <param name="retryAfter">Optional server-requested wait before retrying (e.g. a Retry-After header).</param>
    public UsageUnavailableException(
        string reason, bool isTransient, Exception? innerException = null, TimeSpan? retryAfter = null)
        : base(reason, innerException)
    {
        Reason = reason;
        IsTransient = isTransient;
        RetryAfter = retryAfter;
    }

    /// <summary>Short, user-facing explanation of why usage is unavailable.</summary>
    public string Reason { get; }

    /// <summary>Whether the condition is transient (stale) rather than hard (unavailable).</summary>
    public bool IsTransient { get; }

    /// <summary>Server-requested wait before retrying (e.g. from a Retry-After header), when provided.</summary>
    public TimeSpan? RetryAfter { get; }
}
