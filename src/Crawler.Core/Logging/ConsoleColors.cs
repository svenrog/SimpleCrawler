namespace Crawler.Core.Logging;
internal readonly struct ConsoleColors(ConsoleColor? foreground, ConsoleColor? background)
{
    public ConsoleColor? Foreground { get; } = foreground;

    public ConsoleColor? Background { get; } = background;
}
