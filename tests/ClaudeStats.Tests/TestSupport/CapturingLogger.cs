using Microsoft.Extensions.Logging;

namespace ClaudeStats.Tests.TestSupport;

/// <summary>
/// Test <see cref="ILogger{T}"/> that records the formatted text of every entry it receives,
/// so tests can assert on what diagnostics would be written.
/// </summary>
/// <typeparam name="T">The category type.</typeparam>
public sealed class CapturingLogger<T> : ILogger<T>
{
    /// <summary>The formatted messages received, in order.</summary>
    public List<string> Messages { get; } = [];

    /// <summary>Scopes are not tracked.</summary>
    /// <typeparam name="TState">The scope state type.</typeparam>
    /// <param name="state">The scope state.</param>
    /// <returns>Always null.</returns>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <summary>All levels are enabled for capture.</summary>
    /// <param name="logLevel">The level to test.</param>
    /// <returns>Always true.</returns>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <summary>Records the formatted message.</summary>
    /// <typeparam name="TState">The state type.</typeparam>
    /// <param name="logLevel">The entry level.</param>
    /// <param name="eventId">The event id.</param>
    /// <param name="state">The state to format.</param>
    /// <param name="exception">The associated exception, if any.</param>
    /// <param name="formatter">The formatter rendering the entry.</param>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
}
