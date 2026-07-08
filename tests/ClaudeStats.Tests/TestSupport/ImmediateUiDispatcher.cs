using ClaudeStats.Services;

namespace ClaudeStats.Tests.TestSupport;

/// <summary>Test <see cref="IUiDispatcher"/> that runs work inline on the calling thread.</summary>
public sealed class ImmediateUiDispatcher : IUiDispatcher
{
    /// <inheritdoc />
    public Task InvokeAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
