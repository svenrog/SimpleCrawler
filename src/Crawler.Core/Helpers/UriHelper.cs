namespace Crawler.Core.Helpers;

public static class UriHelper
{
    public static string? GetAbsoluteUrl(Uri baseUri, string? href)
    {
        if (!Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out var uri))
            return null;

        if (!uri.IsAbsoluteUri)
            uri = new Uri(baseUri, uri);

        return uri.ToString();
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
