namespace SimpleCrawler.Core.Helpers;

public static class UriHelper
{
    public static string? GetAbsoluteUrl(Uri baseUri, string? href)
    {
        if (href is null)
            return null;

        if (Uri.TryCreate(href, UriKind.Absolute, out var absolute))
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
