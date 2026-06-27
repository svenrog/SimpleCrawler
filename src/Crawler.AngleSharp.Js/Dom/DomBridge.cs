using Crawler.AngleSharp.Js.Dom.Network;
using Crawler.AngleSharp.Js.Dom.Window;
using Crawler.AngleSharp.Js.Models;

namespace Crawler.AngleSharp.Js.Dom;

public sealed class DomBridge
{
    private readonly DomContext _context;
    private readonly Viewport _viewport;
    private int _handle;

    internal DomBridge(DomContext context, Viewport viewport)
    {
        _context = context;
        _viewport = viewport;
    }

    public object? SetTimeout(params object?[] args) => Schedule(args);
    public object? RequestAnimationFrame(params object?[] args) => Schedule(args);

    public object MatchMedia(params object?[] args)
    {
        var query = args.Length > 0 ? args[0]?.ToString() ?? string.Empty : string.Empty;
        return new JsMediaQueryList(query, _viewport);
    }

    // A crawl has no layout engine, so computed style is just the element's inline declarations (and ""
    // for everything unset, which is what getPropertyValue already returns). Layout libraries read it to
    // probe values; they only need it to exist and not throw.
    public object GetComputedStyle(params object?[] args)
    {
        var element = args.Length > 0 && args[0] is JsElement wrapper ? wrapper.Element : _context.Document.CreateElement("div");
        return new JsStyle(element);
    }

    public object? QueueMicrotask(params object?[] args)
    {
        if (args.Length > 0 && args[0] is { } callback)
            _context.Enqueue(callback);

        return null;
    }

    // Backs the Symbol.hasInstance of the DOM/Web globals exposed as JS shims. A CLR host type embedded
    // with AddHostType has no JS .prototype, so `x instanceof Element` throws on V8 ("non-object prototype
    // undefined") instead of testing the wrapper's type — so instanceof is answered here by CLR type.
    public object IsInstance(params object?[] args)
    {
        var value = args.Length > 0 ? args[0] : null;
        var kind = args.Length > 1 ? args[1]?.ToString() : null;
        return kind switch
        {
            "Node" => value is JsNode,
            "Element" => value is JsElement,
            "Text" => value is JsText,
            "Document" => value is JsDocument,
            "Event" => value is JsEvent,
            "CustomEvent" => value is JsCustomEvent,
            "URL" => value is JsUrl,
            "AbortController" => value is JsAbortController,
            "AbortSignal" => value is JsAbortSignal,
            _ => false,
        };
    }

    public object? SetInterval(params object?[] args) => (double)0;
    public object? ReturnTrue(params object?[] args) => true;
    public object? Noop(params object?[] args) => null;

    private object Schedule(object?[] args)
    {
        if (args.Length > 0 && args[0] is { } callback)
            _context.Enqueue(callback);

        return (double)++_handle;
    }
}
