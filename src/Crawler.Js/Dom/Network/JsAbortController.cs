namespace Crawler.Js.Dom.Network;

// A no-op AbortController paired with JsAbortSignal: SPAs construct one per fetch and pass its signal,
// but a synchronous render never aborts, so abort() is a no-op.
public sealed class JsAbortController
{
    public JsAbortSignal signal { get; } = new();

    public void abort(object? reason = null) { }
}
