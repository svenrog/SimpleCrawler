using Crawler.AngleSharp.Js.Abstractions;

namespace Crawler.AngleSharp.Js.Dom.Helpers;

internal class LocationHelper
{
    public static void Apply(IJsLocation location, string url, bool includeHref = true)
    {
        if (Uri.TryCreate(new Uri(location.href), url, out var resolved))
            Apply(location, resolved, includeHref);
    }

    public static void Apply(IJsLocation location, Uri uri, bool includeHref = true)
    {
        if (includeHref)
            location.href = uri.AbsoluteUri;

        location.origin = $"{uri.Scheme}://{uri.Authority}";
        location.protocol = uri.Scheme + ":";
        location.host = uri.Authority;
        location.hostname = uri.Host;
        location.port = uri.IsDefaultPort ? string.Empty : uri.Port.ToString();
        location.pathname = uri.AbsolutePath;
        location.search = uri.Query;
        location.hash = uri.Fragment;
    }
}
