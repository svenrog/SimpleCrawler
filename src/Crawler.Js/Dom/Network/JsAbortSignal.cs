namespace Crawler.Js.Dom.Network;

// A no-op AbortSignal: render-time fetches are synchronous and never aborted, so it only has to be
// constructible and expose the shape the bundle reads. Mirrors the other no-op observer host types.
public sealed class JsAbortSignal
{
    public bool aborted => false;
    public object? reason => null;

    public void throwIfAborted() { }
    public void addEventListener(object? type = null, object? listener = null, object? options = null) { }
    public void removeEventListener(object? type = null, object? listener = null, object? options = null) { }
    public bool dispatchEvent(object? evt = null) => true;
}
