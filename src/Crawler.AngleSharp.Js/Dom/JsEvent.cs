namespace Crawler.AngleSharp.Js.Dom;

// Constructible Event so bundles can run `new Event(type)` without a ReferenceError. dispatchEvent is a
// no-op, so listeners never observe it; only `type` is carried. Two explicit constructors rather than an
// optional parameter — the engines resolve host-type constructor overloads but not default arguments.
public class JsEvent
{
    public JsEvent(string type) : this(type, null)
    {
    }

    public JsEvent(string type, object? init)
    {
        this.type = type;
    }

    public string type { get; }
    public bool bubbles { get; }
    public bool cancelable { get; }
    public bool defaultPrevented { get; private set; }

    public void preventDefault() => defaultPrevented = true;
    public void stopPropagation() { }
    public void stopImmediatePropagation() { }
}
