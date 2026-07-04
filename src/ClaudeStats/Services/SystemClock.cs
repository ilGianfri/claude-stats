namespace ClaudeStats.Services;

/// <summary>
/// Default <see cref="IClock"/> implementation backed by the system clock.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset Now => DateTimeOffset.Now;
}
