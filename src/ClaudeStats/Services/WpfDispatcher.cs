using System.Windows;

namespace ClaudeStats.Services;

/// <summary>
/// WPF implementation of <see cref="IUiDispatcher"/> backed by the application dispatcher.
/// </summary>
public sealed class WpfDispatcher : IUiDispatcher
{
    /// <inheritdoc />
    public Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Application? application = Application.Current;
        if (application is null)
        {
            // No running application (e.g. during shutdown) — run inline.
            action();
            return Task.CompletedTask;
        }

        if (application.Dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return application.Dispatcher.InvokeAsync(action).Task;
    }
}
