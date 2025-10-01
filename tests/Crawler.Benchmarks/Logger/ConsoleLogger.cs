using BenchmarkDotNet.Loggers;
using Microsoft.Extensions.Logging;
using System;
using BenchmarksConsoleLogger = BenchmarkDotNet.Loggers.ConsoleLogger;
using IAbstractionsLogger = Microsoft.Extensions.Logging.ILogger;
using IBenchmarksLogger = BenchmarkDotNet.Loggers.ILogger;

namespace Crawler.Benchmarks.Logger;

internal class ConsoleLogger : IAbstractionsLogger
{
    private static readonly IBenchmarksLogger _logger = BenchmarksConsoleLogger.Default;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        throw new NotImplementedException();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        var message = formatter(state, exception);
        switch (logLevel)
        {
            case LogLevel.Trace: _logger.WriteLineHint(message); return;
            case LogLevel.Debug: _logger.WriteLineHint(message); return;
            case LogLevel.Information: _logger.WriteLineInfo(message); return;
            case LogLevel.Warning: _logger.WriteLineWarning(message); return;
            case LogLevel.Error: _logger.WriteLineError(message); return;
            case LogLevel.Critical: _logger.WriteLineError(message); return;
            default: _logger.WriteLine(message); return;
        }
    }
}
