namespace Crawler.AngleSharp.Js.Dom.Window;

// A no-op MediaQueryList: every query reports unmatched (a crawl has no viewport), but the object is
// shaped like the real one so SPAs that read .matches or attach change listeners render.
public sealed class JsMediaQueryList
{
    public JsMediaQueryList(string query)
    {
        media = query;
    }

    public string media { get; }
    public bool matches => false;
    public object? onchange { get; set; }

    public void addEventListener(object? type = null, object? listener = null, object? options = null) { }
    public void removeEventListener(object? type = null, object? listener = null, object? options = null) { }
    public void addListener(object? listener = null) { }
    public void removeListener(object? listener = null) { }
    public bool dispatchEvent(object? @event = null) => false;
}
