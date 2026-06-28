using Crawler.AngleSharp.Js.Abstractions;

namespace Crawler.AngleSharp.Js.Dom;

// These properties are for Axios and have to do with HTMLAnchorElement works
// Since there is no separation on specific HTML element types we just monkey patch the behaviour
// Normally you can change the protocol and have that reflected in the href, but that is not our concern

public partial class JsElement : JsNode, IJsLocation
{
    public Uri? Url()
    {
        if (!Uri.TryCreate(Context.Location.href, UriKind.Absolute, out var baseUri))
            return null;

        return Uri.TryCreate(baseUri, href, out var uri) ? uri : null;
    }

    public string href
    {
        get => Element.GetAttribute("href") ?? string.Empty;
        set => Element.SetAttribute("href", value ?? string.Empty);
    }

    public string protocol
    {
        get => Element.GetAttribute("protocol") ?? Url()?.Scheme ?? string.Empty;
        set => Element.SetAttribute("protocol", value ?? string.Empty);
    }

    public string host
    {
        get => Element.GetAttribute("host") ?? Url()?.Authority ?? string.Empty;
        set => Element.SetAttribute("host", value ?? string.Empty);
    }

    public string hostname
    {
        get => Element.GetAttribute("hostname") ?? Url()?.Host ?? string.Empty;
        set => Element.SetAttribute("hostname", value ?? string.Empty);
    }

    public string port
    {
        get
        {
            var port = Element.GetAttribute("port");
            if (port != null) return port;
            var uri = Url();
            if (uri == null || uri.IsDefaultPort) return string.Empty;
            return uri.Port.ToString();
        }
        set => Element.SetAttribute("port", value ?? string.Empty);
    }

    public string pathname
    {
        get => Element.GetAttribute("pathname") ?? Url()?.AbsolutePath ?? string.Empty;
        set => Element.SetAttribute("pathname", value ?? string.Empty);
    }

    public string search
    {
        get => Element.GetAttribute("search") ?? Url()?.Query ?? string.Empty;
        set => Element.SetAttribute("search", value ?? string.Empty);
    }

    public string origin
    {
        get
        {
            var origin = Element.GetAttribute("origin");
            if (origin != null) return origin;
            var uri = Url();
            if (uri == null) return string.Empty;
            return $"{uri.Scheme}://{uri.Authority}";
        }
        set => Element.SetAttribute("origin", value ?? string.Empty);
    }

    public string hash
    {
        get => Element.GetAttribute("hash") ?? Url()?.Fragment ?? string.Empty;
        set => Element.SetAttribute("hash", value ?? string.Empty);
    }
}
