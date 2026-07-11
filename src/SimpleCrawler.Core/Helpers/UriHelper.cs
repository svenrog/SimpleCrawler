using System.Diagnostics.CodeAnalysis;

namespace SimpleCrawler.Core.Helpers;

public static class UriHelper
{
    /// <summary>
    /// On Unix, Uri.TryCreate("/path", Absolute) succeeds as a file:// URI, so a plain absolute-parse
    /// misreads root-relative paths as external file URLs. Only http/https are crawlable absolutes;
    /// anything else must be resolved against a base URI.
    /// </summary>
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

    /// <summary>
    /// Canonicalizes an absolute http(s) URL for deduplication: drops the fragment, lowercases scheme and
    /// host, removes the default port, and collapses a trailing slash (except on the root path). The query
    /// string is preserved verbatim. Returns the input unchanged when it is not an absolute http(s) URL.
    /// </summary>
    public static string Normalize(string url)
    {
        if (!TryCreateHttpAbsolute(url, out var uri))
            return url;

        var path = uri.AbsolutePath;
        if (path.Length > 1 && path.EndsWith('/'))
            path = path.TrimEnd('/');

        return $"{uri.Scheme}://{uri.Authority}{path}{uri.Query}";
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
