using Microsoft.Extensions.Logging;

namespace Age.Core;

/// <summary>
/// Sends log records to an <see cref="IConsoleService"/>, which is what makes the console of a running build show what
/// the engine and the game reported.
/// </summary>
/// <remarks>
/// The levels and the filters belong to the logging builder that the provider is given to, so a record that reaches this
/// provider is one that the builder already let through. Everything at <see cref="LogLevel.Error"/> and above is written
/// as an error, so that a failure stands out where the console is drawn.
/// </remarks>
/// <example>
/// <code>
/// services.AddLogging(builder =&gt; builder.AddProvider(new ConsoleLoggerProvider(console)));
/// </code>
/// </example>
public sealed class ConsoleLoggerProvider : ILoggerProvider
{
    private readonly IConsoleService _console;

    /// <summary>Initializes the provider with the console that the records are written to.</summary>
    /// <param name="console">The console to write to.</param>
    /// <exception cref="ArgumentNullException">The console is null.</exception>
    public ConsoleLoggerProvider(IConsoleService console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new ConsoleLogger(_console, categoryName);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>Writes the records of one category to the console, one line each.</summary>
    private sealed class ConsoleLogger : ILogger
    {
        private readonly IConsoleService _console;
        private readonly string _category;

        public ConsoleLogger(IConsoleService console, string category)
        {
            _console = console;
            _category = category;
        }

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string message = formatter(state, exception);

            // A message that already holds the text of the exception does not repeat it, which is what a caller that logs
            // the message of an exception as part of its own gets.
            string line = exception is null || message.Contains(exception.Message, StringComparison.Ordinal)
                ? $"[{logLevel}] {_category}: {message}"
                : $"[{logLevel}] {_category}: {message} ({exception.GetType().Name}: {exception.Message})";

            if (logLevel >= LogLevel.Error)
            {
                _console.WriteError(line);
                return;
            }

            if (logLevel == LogLevel.Warning)
            {
                _console.WriteWarning(line);
                return;
            }

            _console.Write(line);
        }
    }
}
