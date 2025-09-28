namespace Crawler.Core;

internal class Constants
{
    public static readonly List<string> FilterLinkPrefixes =
    [
        "#",
        "mailto:",
        "tel:"
    ];

    public static readonly List<string> AllowedFileTypes =
    [
        ".html",
        ".htm"
    ];
}
