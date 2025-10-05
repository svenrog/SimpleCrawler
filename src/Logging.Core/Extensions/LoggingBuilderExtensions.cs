using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System.Diagnostics.CodeAnalysis;

namespace Logging.Core.Extensions;

public static class LoggingBuilderExtensions
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(CrudeLogFormatter))]
    public static void AddConsoleLogging(this ILoggingBuilder builder, LogLevel? minimumLevel = LogLevel.Debug)
    {
        builder.AddConsole(x =>
        {
            x.FormatterName = CrudeLogFormatter.FormatterName;
        })
        .AddConsoleFormatter<CrudeLogFormatter, ConsoleFormatterOptions>();

        if (minimumLevel.HasValue)
            builder.SetMinimumLevel(minimumLevel.Value);
    }
}
