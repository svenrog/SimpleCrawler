namespace Crawler.AngleSharp.Js.Dom;

public sealed class DomBridge
{
    private readonly DomContext _context;
    private int _handle;

    internal DomBridge(DomContext context)
    {
        _context = context;
    }

    public object? SetTimeout(params object?[] args) => Schedule(args);
    public object? RequestAnimationFrame(params object?[] args) => Schedule(args);

    public object MatchMedia(params object?[] args)
    {
        var query = args.Length > 0 ? args[0]?.ToString() ?? string.Empty : string.Empty;
        return new JsMediaQueryList(query);
    }

    public object? QueueMicrotask(params object?[] args)
    {
        if (args.Length > 0 && args[0] is { } callback)
            _context.Enqueue(callback);

        return null;
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
