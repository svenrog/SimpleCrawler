namespace Crawler.AngleSharp.Js.Dom.Window;

// A no-op custom-element registry: define/get/whenDefined exist so SPAs that register web components
// render, but nothing is upgraded — there is no HTMLElement base in this engine, so the bundle's own
// element classes never construct in the first place.
public sealed class JsCustomElements
{
    public void define(object? name = null, object? constructor = null, object? options = null) { }
    public object? get(object? name = null) => null;
    public object? whenDefined(object? name = null) => null;
    public void upgrade(object? root = null) { }
}
