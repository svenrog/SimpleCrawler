namespace Crawler.AngleSharp.Js.Dom.Window;

public sealed class JsUrl
{
    public JsUrl(string url) : this(url, null)
    {
    }

    public JsUrl(string url, string? @base)
    {
        var uri = string.IsNullOrEmpty(@base) ? new Uri(url, UriKind.Absolute) : new Uri(new Uri(@base), url);
        origin = $"{uri.Scheme}://{uri.Authority}";
        protocol = uri.Scheme + ":";
        host = uri.Authority;
        hostname = uri.Host;
        port = uri.IsDefaultPort ? string.Empty : uri.Port.ToString();
        pathname = uri.AbsolutePath;
        hash = uri.Fragment;
        searchParams = new JsUrlSearchParams(uri.Query);
    }

    public string origin { get; }
    public string protocol { get; }
    public string host { get; }
    public string hostname { get; }
    public string port { get; }
    public string pathname { get; }
    public string hash { get; }

    // A live view backed by searchParams, so a bundle that does `url.searchParams.set(...)` then
    // `fetch(url)` (which reads href/toString) sees the mutated query.
    public string search
    {
        get
        {
            var query = searchParams.ToString();
            return query.Length == 0 ? string.Empty : "?" + query;
        }
    }

    public string href => $"{protocol}//{host}{pathname}{search}{hash}";

    public JsUrlSearchParams searchParams { get; }

    public override string ToString() => href;
}
