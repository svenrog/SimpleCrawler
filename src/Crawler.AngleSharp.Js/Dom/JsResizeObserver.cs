namespace Crawler.AngleSharp.Js.Dom;

// A no-op ResizeObserver: constructible and callable so SPAs that observe elements render, but it never
// reports resizes (there is no layout during a crawl). Mirrors JsIntersectionObserver.
public sealed class JsResizeObserver
{
    public JsResizeObserver(object? callback)
    {
    }

    public void observe(object? target = null, object? options = null) { }
    public void unobserve(object? target = null) { }
    public void disconnect() { }
}
