namespace Crawler.AngleSharp.Js.Dom;

// A no-op IntersectionObserver: constructible and callable so SPAs that observe elements render, but
// it never reports intersections (there is no viewport to intersect with during a crawl). Two explicit
// constructors rather than an optional parameter — the engines resolve host-type constructor overloads
// but not their default arguments (see JsUrl).
public sealed class JsIntersectionObserver
{
    public JsIntersectionObserver(object? callback) : this(callback, null)
    {
    }

    public JsIntersectionObserver(object? callback, object? options)
    {
    }

    public void observe(object? target = null, object? options = null) { }
    public void unobserve(object? target = null) { }
    public void disconnect() { }
}
