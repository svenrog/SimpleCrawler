using AngleSharp.Dom;

namespace Crawler.AngleSharp.Js.Dom;

public class JsElement : JsNode
{
    private JsStyle? _style;
    private JsStyleSheet? _sheet;
    private JsRelList? _relList;

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
        set => TrySetDomProperty("className", value);
    }

    public string id
    {
        get => Element.Id ?? string.Empty;
        set => TrySetDomProperty("id", value);
    }

    public string innerHTML
    {
        get => Element.InnerHtml;
        set => TrySetDomProperty("innerHTML", value);
    }

    public string outerHTML => Element.OuterHtml;

    public object style => _style ??= new JsStyle(Element);
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

    protected override bool TrySetDomProperty(string name, object? value)
    {
        switch (name)
        {
            case "className":
                Element.ClassName = value?.ToString() ?? string.Empty;
                return true;
            case "id":
                Element.Id = value?.ToString() ?? string.Empty;
                return true;
            case "innerHTML":
                Element.InnerHtml = value?.ToString() ?? string.Empty;
                return true;
            default:
                return base.TrySetDomProperty(name, value);
        }
    }
}
