namespace Crawler.AngleSharp.Js.Dom;

public sealed class JsRelList
{
    public bool supports(object? token) => true;

    public void add(object? token = null) { }
    public void remove(object? token = null) { }
    public bool toggle(object? token = null) => false;
    public bool contains(object? token = null) => false;
}
