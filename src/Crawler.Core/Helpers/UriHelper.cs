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
}
