namespace Crawler.AngleSharp.Js.Dom;

public sealed class JsCustomEvent : JsEvent
{
    public JsCustomEvent(string type) : base(type)
    {
    }

    public JsCustomEvent(string type, object? init) : base(type, init)
    {
    }

    public object? detail { get; }
}
