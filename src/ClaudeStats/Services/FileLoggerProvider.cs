using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ClaudeStats.Services;

/// <summary>
/// Minimal, dependency-free <see cref="ILoggerProvider"/> that appends entries to a daily log file
/// under <c>%LOCALAPPDATA%\ClaudeStats\logs</c>, so diagnostics can be collected from users in the
/// field. Writes are serialized with a lock and never throw; callers are responsible for not logging
/// secrets (token values are never passed to the logger).
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly object _gate = new();
    private readonly string _directory;
    private readonly LogLevel _minLevel;

    /// <summary>Initializes the provider using the default log directory under <c>%LOCALAPPDATA%</c>.</summary>
    /// <param name="minLevel">The minimum level to write.</param>
    public FileLoggerProvider(LogLevel minLevel)
        : this(minLevel, DefaultDirectory())
    {
    }

    /// <summary>Initializes the provider with an explicit log directory (used by tests).</summary>
    /// <param name="minLevel">The minimum level to write.</param>
    /// <param name="directory">Absolute path to the directory that will hold the daily log files.</param>
    public FileLoggerProvider(LogLevel minLevel, string directory)
    {
        _minLevel = minLevel;
        _directory = directory;
        try
        {
            Directory.CreateDirectory(_directory);
        }
        catch (IOException)
        {
            // If the directory cannot be created, appends below silently no-op.
        }
    }

    /// <summary>Computes the default log directory under the local application-data folder.</summary>
    /// <returns>The absolute path to <c>%LOCALAPPDATA%\ClaudeStats\logs</c>.</returns>
    public static string DefaultDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClaudeStats",
        "logs");

    /// <summary>Gets the absolute path of the current day's log file.</summary>
    public string CurrentLogFilePath =>
        Path.Combine(_directory, $"claudestats-{DateTime.Now:yyyy-MM-dd}.log");

    /// <summary>Creates a logger for the given category.</summary>
    /// <param name="categoryName">The category (typically the owning type's full name).</param>
    /// <returns>A file-backed logger.</returns>
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    /// <summary>Releases resources held by the provider. No-op — files are opened per write.</summary>
    public void Dispose()
    {
    }

    /// <summary>Appends one already-formatted entry to the current day's log file.</summary>
    /// <param name="line">The line to append (including its trailing newline).</param>
    private void Append(string line)
    {
        lock (_gate)
        {
            try
            {
                File.AppendAllText(CurrentLogFilePath, line, Encoding.UTF8);
            }
            catch (IOException)
            {
                // Never let logging crash the app.
            }
        }
    }

    /// <summary>A logger that formats entries and forwards them to the owning provider.</summary>
    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _provider;
        private readonly string _category;

        /// <summary>Initializes the logger.</summary>
        /// <param name="provider">The owning provider.</param>
        /// <param name="category">The logger category name.</param>
        public FileLogger(FileLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        /// <summary>Scopes are not supported by this provider.</summary>
        /// <typeparam name="TState">The scope state type.</typeparam>
        /// <param name="state">The scope state.</param>
        /// <returns>Always null.</returns>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <summary>Indicates whether the given level is enabled for writing.</summary>
        /// <param name="logLevel">The level to test.</param>
        /// <returns>True when the level should be written.</returns>
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= _provider._minLevel;

        /// <summary>Formats and appends a single log entry.</summary>
        /// <typeparam name="TState">The state type.</typeparam>
        /// <param name="logLevel">The entry level.</param>
        /// <param name="eventId">The event id.</param>
        /// <param name="state">The state to format.</param>
        /// <param name="exception">The associated exception, if any.</param>
        /// <param name="formatter">The formatter that renders <paramref name="state"/> and <paramref name="exception"/>.</param>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string message = formatter(state, exception);
            StringBuilder builder = new();
            builder.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
            builder.Append(" [").Append(Level(logLevel)).Append("] ");
            builder.Append(_category).Append(" - ").Append(message);
            if (exception is not null)
            {
                builder.Append(Environment.NewLine).Append(exception);
            }

            builder.Append(Environment.NewLine);
            _provider.Append(builder.ToString());
        }

        /// <summary>Maps a log level to a short, fixed-width tag.</summary>
        /// <param name="level">The level to map.</param>
        /// <returns>A short tag for the level.</returns>
        private static string Level(LogLevel level) => level switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO ",
            LogLevel.Warning => "WARN ",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRIT ",
            _ => "     ",
        };
    }
}
