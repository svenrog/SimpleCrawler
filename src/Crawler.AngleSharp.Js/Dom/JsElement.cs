using AngleSharp.Dom;
using Crawler.AngleSharp.Js.Abstractions;
using Crawler.AngleSharp.Js.Dom.Helpers;

namespace Crawler.AngleSharp.Js.Dom;

public class JsElement : JsNode, IJsLocation
{
    private JsStyle? _style;
    private JsStyleSheet? _sheet;
    private JsRelList? _relList;
    private JsDataset? _dataset;

    internal JsElement(IElement element, DomContext context) : base(element, context)
    {
    }
    internal IElement Element => (IElement)Node;

    public string tagName => Element.TagName;
    public string localName => Element.LocalName;
    public string namespaceURI => Element.NamespaceUri ?? string.Empty;

    public string className
    {
        get => Element.ClassName ?? string.Empty;
        set => Element.ClassName = value ?? string.Empty;
    }

    public string id
    {
        get => Element.Id ?? string.Empty;
        set => Element.Id = value ?? string.Empty;
    }

    public string innerHTML
    {
        get => Element.InnerHtml;
        set => Element.InnerHtml = value ?? string.Empty;
    }

    public string outerHTML => Element.OuterHtml;

    public object children => Context.WrapAll(Element.Children);
    public object? firstElementChild => Context.Wrap(Element.FirstElementChild);
    public object? lastElementChild => Context.Wrap(Element.LastElementChild);
    public int childElementCount => Element.ChildElementCount;

    public object style => _style ??= new JsStyle(Element);
    public object dataset => _dataset ??= new JsDataset(Element);
    public object? sheet => string.Equals(Element.LocalName, "style", StringComparison.Ordinal) ? _sheet ??= new JsStyleSheet(Context) : null;
    public object relList => _relList ??= new JsRelList();

    public void setAttribute(string name, object? value) => Element.SetAttribute(name, value?.ToString() ?? string.Empty);
    public void setAttributeNS(object? namespaceUri, string name, object? value) => Element.SetAttribute(name, value?.ToString() ?? string.Empty);
    public string? getAttribute(string name) => Element.GetAttribute(name);
    public void removeAttribute(string name) => Element.RemoveAttribute(name);
    public bool hasAttribute(string name) => Element.HasAttribute(name);

    public object? querySelector(string selector) => Context.Wrap(Element.QuerySelector(selector));
    public object querySelectorAll(string selector) => Context.WrapAll(Element.QuerySelectorAll(selector));
    public object getElementsByTagName(string name) => Context.WrapAll(Element.GetElementsByTagName(name));
    public object? closest(string selector) => Context.Wrap(Element.Closest(selector));

    // Hacks follow, refactor this later

    // A real (CLR) property, not an expando, so webpack's chunk loader (`script.src=url`) writes the
    // attribute the renderer reads back when it fetches and executes the dynamically appended chunk.
    public string src
    {
        get => Element.GetAttribute("src") ?? string.Empty;
        set => Element.SetAttribute("src", value ?? string.Empty);
    }

    public Uri? Url()
    {
        if (Uri.TryCreate(new Uri(Context.Location.href), href, out Uri? uri))
            return uri;

        return null;
    }

    // These properties are for Axios and have to do with HTMLAnchorElement works
    // Since there is no separation on specific HTML element types we just monkey patch the behaviour
    // Normally you can change the protocol and have that reflected in the href, but that is not our concern
    public string href
    {
        get => Element.GetAttribute("href") ?? string.Empty;
        set
        {
            LocationHelper.Apply(this, value, includeHref: false);
            Element.SetAttribute("href", value ?? string.Empty);
        }
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
