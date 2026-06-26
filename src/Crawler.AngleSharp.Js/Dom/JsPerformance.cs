using System.Diagnostics;

namespace Crawler.AngleSharp.Js.Dom;

public sealed class JsPerformance
{
    private readonly DomContext _context;
    private readonly long _start = Stopwatch.GetTimestamp();

    public JsPerformance(DomContext context)
    {
        _context = context;
    }

    public double timeOrigin => 0;

    public double now() => Stopwatch.GetElapsedTime(_start).TotalMilliseconds;

    public void mark(params object?[] args) { }
    public void measure(params object?[] args) { }
    public void clearMarks(params object?[] args) { }
    public void clearMeasures(params object?[] args) { }

    // web-vitals iterates these, and a bare .NET array reaches V8 without a JS `length`, so build a real
    // JS array through the engine (see DomContext.CreateArray).
    public object getEntries() => _context.CreateArray([]);
    public object getEntriesByName(params object?[] args) => _context.CreateArray([]);
    public object getEntriesByType(params object?[] args) => _context.CreateArray([]);
}
