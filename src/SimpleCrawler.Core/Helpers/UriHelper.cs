using System.Diagnostics.CodeAnalysis;

namespace SimpleCrawler.Core.Helpers;

public static class UriHelper
{
    // On Unix, Uri.TryCreate("/path", Absolute) succeeds as a file:// URI, so a plain absolute-parse
    // misreads root-relative paths as external file URLs. Only http/https are crawlable absolutes;
    // anything else must be resolved against a base URI.
    public static bool TryCreateHttpAbsolute(string? value, [NotNullWhen(true)] out Uri? absolute)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps);
    }

    public static string? GetAbsoluteUrl(Uri baseUri, string? href)
    {
        if (href is null)
            return null;

        if (TryCreateHttpAbsolute(href, out var absolute))
            return absolute.ToString();

        if (!Uri.TryCreate(href, UriKind.Relative, out var relative))
            return null;

        return new Uri(baseUri, relative).ToString();
    }

    public static List<string> GetAbsoluteUrls(Uri baseUri, IEnumerable<string> hrefs)
    {
        var links = new List<string>();

        foreach (var href in hrefs)
        {
            var link = GetAbsoluteUrl(baseUri, href);
            if (link == null)
                continue;

            links.Add(link);
        }

        return links;
    }
}
