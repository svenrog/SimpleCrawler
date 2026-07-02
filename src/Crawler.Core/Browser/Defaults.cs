namespace Crawler.Core.Browser;

internal static class Defaults
{
    public const string UserAgent = "SimpleCrawler/1.0 (+https://github.com/svenrog/simpleCrawler)";

    public const string Locale = "en-US";

    public const string Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";

    public const string AcceptLanguage = "en-US,en;q=0.9";

    public static readonly Dictionary<string, string> AdditionalHeaders = [];
}
