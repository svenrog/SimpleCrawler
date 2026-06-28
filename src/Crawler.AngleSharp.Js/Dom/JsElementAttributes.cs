using Crawler.AngleSharp.Js.Abstractions;
using Crawler.AngleSharp.Js.Dom.Helpers;

namespace Crawler.AngleSharp.Js.Dom;

// Reflected attribute properties shared by several element types. jQuery's load-time support detection
// sets input.type and reads .value; analytics loaders (GTM/Clerk) set script.async/.type/.defer. Without
// these setters the engines throw "no suitable property or field named ...", aborting the script before it
// finishes — for jQuery that means window.jQuery is never assigned, so every later bundle that reads the
// jQuery global fails with "jQuery is not defined". The setters take object? and coerce because the DOM
// reflects these loosely (e.g. GTM assigns `script.async = 1`), and a typed setter would reject the number.

public partial class JsElement : JsNode, IJsLocation
{
    public object type
    {
        get => Element.GetAttribute("type") ?? string.Empty;
        set => Element.SetAttribute("type", value?.ToString() ?? string.Empty);
    }

    public object value
    {
        get => Element.GetAttribute("value") ?? string.Empty;
        set => Element.SetAttribute("value", value?.ToString() ?? string.Empty);
    }

    public object async
    {
        get => Element.HasAttribute("async");
        set => ToggleAttribute("async", JsValue.IsTruthy(value));
    }

    public object defer
    {
        get => Element.HasAttribute("defer");
        set => ToggleAttribute("defer", JsValue.IsTruthy(value));
    }

    private void ToggleAttribute(string name, bool present)
    {
        if (present)
            Element.SetAttribute(name, string.Empty);
        else
            Element.RemoveAttribute(name);
    }
}
