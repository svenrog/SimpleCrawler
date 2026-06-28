using Crawler.AngleSharp.Js.Dom.Helpers;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;

namespace Crawler.AngleSharp.Js.Dom.Window.Logging;

public sealed class LoggingJsConsole : IJsConsole
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, long> _timers = new();
    private readonly ConcurrentDictionary<string, int> _counters = new();
    private int _groupDepth = 0;

    public LoggingJsConsole(ILogger logger)
    {
        _logger = logger;
    }

    public void assert(params object?[] args)
    {
        // console.assert(condition, ...msg) — logs only when condition is falsy
        bool condition = args.Length > 0 && JsValue.IsTruthy(args[0]);
        if (!condition)
        {
            var msg = args.Length > 1
                ? "Assertion failed: " + FormatArgs(args[1..])
                : "Assertion failed";
            Log(LogLevel.Error, msg);
        }
    }

    public void debug(params object?[] args) => Log(LogLevel.Debug, FormatArgs(args));
    public void info(params object?[] args) => Log(LogLevel.Information, FormatArgs(args));
    public void log(params object?[] args) => Log(LogLevel.Information, FormatArgs(args));
    public void warn(params object?[] args) => Log(LogLevel.Warning, FormatArgs(args));
    public void error(params object?[] args) => Log(LogLevel.Error, FormatArgs(args));
    public void trace(params object?[] args) => Log(LogLevel.Trace, FormatArgs(args));
    public void dir(params object?[] args) => Log(LogLevel.Debug, FormatArgs(args));
    public void dirxml(params object?[] args) => Log(LogLevel.Debug, FormatArgs(args));

    public void group(params object?[] args)
    {
        var label = args.Length > 0 ? FormatArgs(args) : string.Empty;
        Log(LogLevel.Debug, $"▶ {label}");
        Interlocked.Increment(ref _groupDepth);
    }

    public void groupCollapsed(params object?[] args) => group(args); // same treatment

    public void groupEnd(params object?[] args)
    {
        if (_groupDepth > 0)
            Interlocked.Decrement(ref _groupDepth);
    }

    public void count(params object?[] args)
    {
        var label = args.Length > 0 ? Stringify(args[0]) : "default";
        var value = _counters.AddOrUpdate(label, 1, (_, n) => n + 1);
        Log(LogLevel.Debug, $"{label}: {value}");
    }

    public void countReset(params object?[] args)
    {
        var label = args.Length > 0 ? Stringify(args[0]) : "default";
        if (!_counters.TryGetValue(label, out _))
            Log(LogLevel.Warning, $"Count for '{label}' does not exist");
        else
            _counters[label] = 0;
    }

    public void time(params object?[] args)
    {
        var label = args.Length > 0 ? Stringify(args[0]) : "default";
        var now = Environment.TickCount64;
        if (!_timers.TryAdd(label, now))
            Log(LogLevel.Warning, $"Timer '{label}' already exists");
    }

    public void timeLog(params object?[] args)
    {
        var label = args.Length > 0 ? Stringify(args[0]) : "default";
        if (_timers.TryGetValue(label, out var start))
        {
            var elapsed = Environment.TickCount64 - start;
            var extra = args.Length > 1 ? " " + FormatArgs(args[1..]) : string.Empty;
            Log(LogLevel.Debug, $"{label}: {elapsed}ms{extra}");
        }
        else
        {
            Log(LogLevel.Warning, $"Timer '{label}' does not exist");
        }
    }

    public void timeEnd(params object?[] args)
    {
        var label = args.Length > 0 ? Stringify(args[0]) : "default";
        if (_timers.TryRemove(label, out var start))
        {
            var elapsed = Environment.TickCount64 - start;
            Log(LogLevel.Debug, $"{label}: {elapsed}ms - timer ended");
        }
        else
        {
            Log(LogLevel.Warning, $"Timer '{label}' does not exist");
        }
    }

    public void table(params object?[] args)
    {
        // Best-effort; ClearScript objects can be iterated if needed
        Log(LogLevel.Debug, args.Length > 0 ? Stringify(args[0]) : "(empty table)");
    }

    public void clear(params object?[] args) { /* no terminal to clear */ }

    private void Log(LogLevel level, string message)
    {
        if (!_logger.IsEnabled(level)) return;
        var indent = _groupDepth > 0 ? new string(' ', _groupDepth * 2) : string.Empty;
        _logger.Log(level, "{Indent}{Message}", indent, message);
    }

    private static string FormatArgs(object?[] args)
    {
        if (args.Length == 0) return string.Empty;
        if (args.Length == 1) return Stringify(args[0]);

        var fmt = Stringify(args[0]);

        // Only attempt substitution when the first arg looks like a format string
        if (fmt.Contains('%'))
        {
            var sb = new StringBuilder();
            int argIdx = 1;
            int i = 0;

            while (i < fmt.Length)
            {
                if (fmt[i] == '%' && i + 1 < fmt.Length && argIdx < args.Length)
                {
                    char spec = fmt[i + 1];
                    switch (spec)
                    {
                        case 's':
                            sb.Append(Stringify(args[argIdx++]));
                            i += 2;
                            continue;
                        case 'd':
                        case 'i':
                            sb.Append(ToInt(args[argIdx++]));
                            i += 2;
                            continue;
                        case 'f':
                            sb.Append(ToFloat(args[argIdx++]));
                            i += 2;
                            continue;
                        case 'o':
                        case 'O':
                            sb.Append(Stringify(args[argIdx++]));
                            i += 2;
                            continue;
                        case '%':
                            sb.Append('%');
                            i += 2;
                            continue;
                    }
                }
                sb.Append(fmt[i++]);
            }

            while (argIdx < args.Length)
            {
                sb.Append(' ');
                sb.Append(Stringify(args[argIdx++]));
            }

            return sb.ToString();
        }

        return string.Join(" ", args.Select(Stringify));
    }

    private static string Stringify(object? value) => value switch
    {
        null => "null",
        bool b => b ? "true" : "false",
        string s => s,
        _ => value.ToString() ?? "undefined"
    };

    private static long ToInt(object? v) => v is IConvertible c ? c.ToInt64(null) : 0;
    private static double ToFloat(object? v) => v is IConvertible c ? c.ToDouble(null) : 0d;
}