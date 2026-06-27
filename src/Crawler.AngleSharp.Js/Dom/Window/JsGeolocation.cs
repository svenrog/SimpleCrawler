namespace Crawler.AngleSharp.Js.Dom.Window;

// Exposed so SPAs that probe navigator.geolocation render, but the success/error callbacks are never
// delivered: a crawl must not depend on asynchronous, permission-gated position fixes (real headless
// browsers reject them too).
public sealed class JsGeolocation
{
    private int _watch;

    public void getCurrentPosition(object? success = null, object? error = null, object? options = null) { }
    public object watchPosition(object? success = null, object? error = null, object? options = null) => (double)++_watch;
    public void clearWatch(object? id = null) { }
}
