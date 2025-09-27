namespace Logging.Core.Extensions;

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

    public static void WriteHighlightedMessage(this TextWriter textWriter, string message, ConsoleColor background, ConsoleColor foreground, ConsoleColor highlight)
    {
        var messageSpan = message.AsSpan();
        var sections = messageSpan.Split('\'');

        var i = 0;

        foreach (var section in sections)
        {
            var fgcolor = i % 2 == 0 ? foreground : highlight;
            var buffer = messageSpan[section.Start.Value..section.End.Value];

            WriteColoredBuffer(textWriter, buffer, background, fgcolor);
            i++;
        }
    }

    private static void WriteColoredBuffer(this TextWriter textWriter, ReadOnlySpan<char> buffer, ConsoleColor background, ConsoleColor foreground)
    {
        textWriter.Write(AnsiParser.GetBackgroundColorEscapeCode(background));
        textWriter.Write(AnsiParser.GetForegroundColorEscapeCode(foreground));

        textWriter.Write(buffer);

        textWriter.Write(AnsiParser._defaultForegroundColor);
        textWriter.Write(AnsiParser._defaultBackgroundColor);
    }
}