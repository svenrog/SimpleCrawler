using AngleSharp.Dom;

namespace Crawler.AngleSharp.Js.Dom;

public sealed class JsDocument : JsNode
{
    private readonly Dictionary<string, string> _cookies = new(StringComparer.Ordinal);

    internal JsDocument(IDocument document, DomContext context) : base(document, context)
    {
    }

    internal IDocument Document => (IDocument)Node;

    public object? documentElement => Context.Wrap(Document.DocumentElement);
    public object? head => Context.Wrap(Document.Head);
    public object? body => Context.Wrap(Document.Body);
    public object? defaultView => null;
    public object styleSheets => Context.CreateArray([]);

    public string cookie
    {
        get => string.Join("; ", _cookies.Select(pair => $"{pair.Key}={pair.Value}"));
        set => TrySetDomProperty("cookie", value);
    }

    public object createElement(string name) => Context.Wrap(Document.CreateElement(name))!;

    // Preact creates every element via createElementNS(ns, tag, options); the third argument must be
    // accepted or the engines reject the call and the whole render silently produces nothing.
    public object createElementNS(object? namespaceUri, string name, object? options = null)
    {
        var ns = namespaceUri?.ToString();
        var element = string.IsNullOrEmpty(ns) ? Document.CreateElement(name) : Document.CreateElement(ns, name);
        return Context.Wrap(element)!;
    }

    public object createTextNode(object? data) => Context.Wrap(Document.CreateTextNode(data?.ToString() ?? string.Empty))!;

    public object? getElementById(string id) => Context.Wrap(Document.GetElementById(id));
    public object getElementsByTagName(string name) => Context.WrapAll(Document.GetElementsByTagName(name));
    public object? querySelector(string selector) => Context.Wrap(Document.QuerySelector(selector));
    public object querySelectorAll(string selector) => Context.WrapAll(Document.QuerySelectorAll(selector));

    // V8 shadows the CLR `cookie` setter with TrySetMember, so the write has to be handled here too.
    protected override bool TrySetDomProperty(string name, object? value)
    {
        if (string.Equals(name, "cookie", StringComparison.Ordinal))
        {
            SetCookie(value?.ToString());
            return true;
        }

        return base.TrySetDomProperty(name, value);
    }

    private void SetCookie(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return;

        var pair = raw.Split(';', 2)[0];
        var separator = pair.IndexOf('=');
        if (separator < 0)
            return;

        var name = pair[..separator].Trim();
        if (name.Length == 0)
            return;

        _cookies[name] = pair[(separator + 1)..].Trim();
    }
}
