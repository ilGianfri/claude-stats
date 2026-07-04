namespace ClaudeStats.Services;

/// <summary>
/// Abstracts marshaling work onto the UI thread so that ViewModels and services
/// remain free of direct <c>System.Windows</c> / <c>Dispatcher</c> references
/// (Constitution IV: testable, UI-agnostic ViewModels).
/// </summary>
public interface IUiDispatcher
{
    /// <summary>Executes the supplied action on the UI thread.</summary>
    /// <param name="action">The work to run on the UI thread.</param>
    /// <returns>A task that completes when the action has run.</returns>
    Task InvokeAsync(Action action);
}
