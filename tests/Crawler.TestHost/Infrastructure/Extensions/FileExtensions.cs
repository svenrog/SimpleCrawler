namespace Crawler.TestHost.Infrastructure.Extensions;

public class FileExtensions
{
    public static readonly Dictionary<string, string> MimeTypes = new()
    {
        { ".html", "text/html" },
        { ".css", "text/css" },
        { ".txt", "text/plain" },
        { ".woff2", "font/woff2" },
        { ".js", "text/javascript" },
        { ".png", "image/png" },
    };
}
