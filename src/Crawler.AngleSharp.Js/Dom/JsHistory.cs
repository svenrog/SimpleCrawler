namespace Crawler.AngleSharp.Js.Dom;

public sealed class JsHistory
{
    private readonly JsLocation _location;

    internal JsHistory(JsLocation location)
    {
        _location = location;
    }

    public double length => 1;
    public object? state => null;

    public void pushState(object? state, object? title, object? url = null) => Navigate(url);
    public void replaceState(object? state, object? title, object? url = null) => Navigate(url);
    public void go(object? delta = null) { }
    public void back() { }
    public void forward() { }

    private void Navigate(object? url)
    {
        var target = url?.ToString();
        if (!string.IsNullOrEmpty(target))
            _location.Apply(target);
    }
}
