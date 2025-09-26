using Crawler.Core.Logging;

namespace Crawler.Core.Extensions;

internal static class TextWriterExtensions
{
    public static void WriteColoredMessage(this TextWriter textWriter, string message, ConsoleColor? background, ConsoleColor? foreground)
    {
        if (background.HasValue)
        {
            textWriter.Write(AnsiParser.GetBackgroundColorEscapeCode(background.Value));
        }

        if (foreground.HasValue)
        {
            textWriter.Write(AnsiParser.GetForegroundColorEscapeCode(foreground.Value));
        }

        textWriter.Write(message);

        if (foreground.HasValue)
        {
            textWriter.Write(AnsiParser._defaultForegroundColor);
        }

        if (background.HasValue)
        {
            textWriter.Write(AnsiParser._defaultBackgroundColor);
        }
    }
}