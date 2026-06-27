namespace Crawler.AngleSharp.Js.Dom.Window;

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

    // Only a real string URL moves location. A `history.replaceState(state, "")` call omits the URL, and
    // ClearScript marshals that missing argument to its Undefined sentinel (a non-null object whose
    // ToString() is "[undefined]") rather than null — so the old `url?.ToString()` applied "[undefined]"
    // as a path, corrupting location to "/[undefined]" and poisoning every URL the router derived from it.
    private void Navigate(object? url)
    {
        if (url is string target && !string.IsNullOrEmpty(target))
            _location.Apply(target);
    }
}
