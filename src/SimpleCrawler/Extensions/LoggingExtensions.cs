using CommandLine;
using Microsoft.Extensions.Logging;

namespace SimpleCrawler.Extensions;

public static class LoggingExtensions
{
    public static void LogCliErrors(this ILogger logger, IEnumerable<Error> errors)
    {
        foreach (var error in errors)
        {
            LogCliError(logger, error);
        }
    }

    public static void LogCliError(this ILogger logger, Error error)
    {
        if (error is UnknownOptionError unknownError)
        {
            logger.LogError("{tag}: {token}", unknownError.Tag, unknownError.Token);
        }
        else if (error is MissingRequiredOptionError missingRequired)
        {
            logger.LogError("{tag}: {name}",
                missingRequired.Tag,
                missingRequired.NameInfo.NameText);
        }
        else
        {
            logger.LogError("{tag}", error.Tag);
        }
    }
}
