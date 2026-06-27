using AngleSharp.Dom;

namespace Crawler.AngleSharp.Js.Dom;

public class JsText : JsNode
{
    internal JsText(IText text, DomContext context) : base(text, context)
    {
    }

    internal IText Text => (IText)Node;

    public string data
    {
        get => Text.Data;
        set => Text.Data = value ?? string.Empty;
    }
}
