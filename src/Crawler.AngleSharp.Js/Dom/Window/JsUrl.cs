namespace Crawler.AngleSharp.Js.Dom.Window;

public sealed class JsUrl
{
    public JsUrl(string url) : this(url, null)
    {
    }

    public JsUrl(string url, string? @base)
    {
        var uri = string.IsNullOrEmpty(@base) ? new Uri(url, UriKind.Absolute) : new Uri(new Uri(@base), url);
        href = uri.AbsoluteUri;
        origin = $"{uri.Scheme}://{uri.Authority}";
        protocol = uri.Scheme + ":";
        host = uri.Authority;
        hostname = uri.Host;
        port = uri.IsDefaultPort ? string.Empty : uri.Port.ToString();
        pathname = uri.AbsolutePath;
        search = uri.Query;
        hash = uri.Fragment;
        searchParams = ParseSearchParams(uri.Query);
    }

    public string href { get; }
    public string origin { get; }
    public string protocol { get; }
    public string host { get; }
    public string hostname { get; }
    public string port { get; }
    public string pathname { get; }
    public string search { get; }
    public string hash { get; }

    // Exposed as an iterable of [key, value] pairs so the bundle's Object.fromEntries(url.searchParams) works.
    public List<object?> searchParams { get; }

    public override string ToString() => href;

    private static List<object?> ParseSearchParams(string query)
    {
        var pairs = new List<object?>();
        var trimmed = query.StartsWith('?') ? query[1..] : query;
        if (trimmed.Length == 0)
            return pairs;

        foreach (var part in trimmed.Split('&'))
        {
            if (part.Length == 0)
                continue;

            var index = part.IndexOf('=');
            var key = index < 0 ? part : part[..index];
            var value = index < 0 ? string.Empty : part[(index + 1)..];
            pairs.Add(new object?[] { Uri.UnescapeDataString(key), Uri.UnescapeDataString(value) });
        }

        return pairs;
    }
}

