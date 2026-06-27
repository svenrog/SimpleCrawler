namespace Crawler.AngleSharp.Js.Dom.Window;

public sealed class JsLocation
{
    internal JsLocation(Uri uri)
    {
        Apply(uri);
    }

    public string href { get; private set; } = string.Empty;
    public string origin { get; private set; } = string.Empty;
    public string protocol { get; private set; } = string.Empty;
    public string host { get; private set; } = string.Empty;
    public string hostname { get; private set; } = string.Empty;
    public string port { get; private set; } = string.Empty;
    public string pathname { get; private set; } = string.Empty;
    public string search { get; private set; } = string.Empty;
    public string hash { get; private set; } = string.Empty;

    public override string ToString() => href;

    internal void Apply(string url)
    {
        if (Uri.TryCreate(new Uri(href), url, out var resolved))
            Apply(resolved);
    }

    internal void Apply(Uri uri)
    {
        href = uri.AbsoluteUri;
        origin = $"{uri.Scheme}://{uri.Authority}";
        protocol = uri.Scheme + ":";
        host = uri.Authority;
        hostname = uri.Host;
        port = uri.IsDefaultPort ? string.Empty : uri.Port.ToString();
        pathname = uri.AbsolutePath;
        search = uri.Query;
        hash = uri.Fragment;
    }
}
