using AngleSharp.Dom;
using Crawler.AngleSharp.Js.Abstractions;

namespace Crawler.AngleSharp.Js.Dom;

public partial class JsElement : JsNode, IJsLocation
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
}
