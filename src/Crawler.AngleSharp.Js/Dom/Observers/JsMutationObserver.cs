namespace Crawler.AngleSharp.Js.Dom.Observers;

// A no-op MutationObserver: constructible and callable so SPAs that watch the DOM render, but it never
// reports mutations (the tree is serialized once, after the drain settles). Mirrors JsIntersectionObserver.
public sealed class JsMutationObserver
{
    public JsMutationObserver(object? callback)
    {
    }

    public void observe(object? target = null, object? options = null) { }
    public void disconnect() { }
}
