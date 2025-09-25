using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using SimpleCrawler.Logging;
using System.Diagnostics.CodeAnalysis;

namespace SimpleCrawler.Extensions;

public static class LoggingBuilderExtensions
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(CrudeLogFormatter))]
    public static void AddConsoleLogging(this ILoggingBuilder builder, LogLevel? minimumLevel = null)
    {
        builder.AddConsole(x =>
        {
            x.FormatterName = CrudeLogFormatter.FormatterName;
        })
        .AddConsoleFormatter<CrudeLogFormatter, ConsoleFormatterOptions>();

#if DEBUG
        minimumLevel ??= LogLevel.Trace;
#endif

#pragma warning disable S2589 // Boolean expressions should not be gratuitous
        if (minimumLevel.HasValue)
            builder.SetMinimumLevel(minimumLevel.Value);
#pragma warning restore S2589 // Boolean expressions should not be gratuitous

    }
}
