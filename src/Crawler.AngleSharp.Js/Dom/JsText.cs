using AngleSharp.Dom;

namespace Crawler.AngleSharp.Js.Dom;

public sealed class JsText : JsNode
{
    internal JsText(IText text, DomContext context) : base(text, context)
    {
    }

    internal IText Text => (IText)Node;

    public string data
    {
        get => Text.Data;
        set => TrySetDomProperty("data", value);
    }

    protected override bool TrySetDomProperty(string name, object? value)
    {
        if (name is "data")
        {
            Text.Data = value?.ToString() ?? string.Empty;
            return true;
        }

        return base.TrySetDomProperty(name, value);
    }
}
