using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using SimpleCrawler.Extensions;

namespace SimpleCrawler.Logging;
public sealed class CrudeLogFormatter() : ConsoleFormatter(FormatterName)
{
    public const string FormatterName = "crude";

    private const string _loglevelPadding = ": ";
    private const string _timeStampFormat = "HH:mm:ss.fff";

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        if (logEntry.State is BufferedLogRecord bufferedRecord)
        {
            string message = bufferedRecord.FormattedMessage ?? string.Empty;
            WriteInternal(textWriter, message, bufferedRecord.LogLevel, bufferedRecord.Exception, bufferedRecord.Timestamp);
        }
        else
        {
            string message = logEntry.Formatter(logEntry.State, logEntry.Exception);
            if (logEntry.Exception == null && message == null)
            {
                return;
            }

            WriteInternal(textWriter, message, logEntry.LogLevel, logEntry.Exception?.ToString(), GetCurrentDateTime());
        }
    }

    private static void WriteInternal(TextWriter textWriter, string message, LogLevel logLevel, string? exception, DateTimeOffset stamp)
    {
        var logLevelColors = GetLogLevelConsoleColors(logLevel);
        var logLevelString = GetLogLevelString(logLevel);

        textWriter.WriteColoredMessage(stamp.ToString(_timeStampFormat), ConsoleColor.Black, ConsoleColor.DarkGray);

        if (logLevelString != null)
        {
            textWriter.Write(' ');
            textWriter.WriteColoredMessage(logLevelString, logLevelColors.Background, logLevelColors.Foreground);
        }

        textWriter.Write(_loglevelPadding);

        var messageColor = GetLogMessageConsoleColor(logLevel);
        textWriter.WriteColoredMessage(message, ConsoleColor.Black, messageColor);

        if (exception != null)
        {
            WriteMessage(textWriter, exception);
        }

        textWriter.Write(Environment.NewLine);
    }

    private static DateTimeOffset GetCurrentDateTime()
    {
        return DateTimeOffset.UtcNow;
    }

    private static void WriteMessage(TextWriter textWriter, string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        textWriter.Write(message);
    }

    private static string GetLogLevelString(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel))
        };
    }


    private static ConsoleColor GetLogMessageConsoleColor(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => ConsoleColor.DarkCyan,
            LogLevel.Debug => ConsoleColor.DarkCyan,
            _ => ConsoleColor.Gray,
        };
    }

    private static ConsoleColors GetLogLevelConsoleColors(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => new ConsoleColors(ConsoleColor.Blue, ConsoleColor.Black),
            LogLevel.Debug => new ConsoleColors(ConsoleColor.Blue, ConsoleColor.Black),
            LogLevel.Information => new ConsoleColors(ConsoleColor.DarkGreen, ConsoleColor.Black),
            LogLevel.Warning => new ConsoleColors(ConsoleColor.Yellow, ConsoleColor.Black),
            LogLevel.Error => new ConsoleColors(ConsoleColor.Black, ConsoleColor.DarkRed),
            LogLevel.Critical => new ConsoleColors(ConsoleColor.White, ConsoleColor.DarkRed),
            _ => new ConsoleColors(null, null)
        };
    }
}
