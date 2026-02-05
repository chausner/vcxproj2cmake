using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace vcxproj2cmake;

public class CustomConsoleFormatter : ConsoleFormatter
{
    public CustomConsoleFormatter() : base(nameof(CustomConsoleFormatter)) { }

    readonly bool colorsEnabled = !Console.IsOutputRedirected && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        string? message = logEntry.Formatter(logEntry.State, logEntry.Exception);

        if (message == null && logEntry.Exception == null)
            return;

        var (backgroundColor, foregroundColor) = colorsEnabled ? GetColorsForLogLevel(logEntry.LogLevel) : (null, null);

        switch (logEntry.LogLevel)
        {
            case LogLevel.Warning:
                textWriter.WriteColored("Warning: ", backgroundColor, foregroundColor);
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                textWriter.WriteColored("Error: ", backgroundColor, foregroundColor);
                break;
        }

        if (message != null)
            textWriter.WriteLineColored(message, backgroundColor, foregroundColor);

        if (logEntry.Exception != null)
        {
            textWriter.WriteLineColored(logEntry.Exception.ToString(), backgroundColor, foregroundColor);

            if (logEntry.Exception.StackTrace != null)
                textWriter.WriteLineColored(logEntry.Exception.StackTrace, backgroundColor, foregroundColor);
        }
    }

    static (ConsoleColor? Background, ConsoleColor? Foreground) GetColorsForLogLevel(LogLevel logLevel) =>
        logLevel switch
        {
            LogLevel.Warning => (null, ConsoleColor.Yellow),
            LogLevel.Error or LogLevel.Critical => (null, ConsoleColor.Red),
            _ => (null, null)
        };
}