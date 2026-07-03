using Jint;
using System.Collections.Concurrent;

namespace Crawler.Js.Jint;

// A Jint Engine is a whole realm — intrinsics, the shims, and (after the first page) the ~90KB dom.js DOM all
// live in it. Unlike V8 there is no isolate/context split, so pooling means reusing the realm itself: a rented
// engine keeps its installed DOM and is reset between pages (JintJsEngine.BeginPage → __crawlerReset) rather
// than rebuilt. That amortizes both the Engine construction and the dom.js re-eval across pages.
//
// An Engine is single-threaded, so a rented engine is held exclusively until the page's wrapper is disposed;
// the bag self-sizes to the crawl's peak concurrency. Cross-page state leaking past the reset is the risk, so
// each engine is retired after a fixed number of pages and replaced with a fresh realm.
internal sealed class JintEnginePool : IDisposable
{
    private readonly ConcurrentBag<JintEngineLease> _leases = [];
    private readonly JintModuleCache _moduleCache;
    private readonly int _maxUsesPerEngine;

    public JintEnginePool(JintEngineOptions options, JintModuleCache moduleCache)
    {
        _moduleCache = moduleCache;
        _maxUsesPerEngine = options.MaxUsesPerEngine > 0 ? options.MaxUsesPerEngine : 0;
    }

    public JintEngineLease Rent()
    {
        return _leases.TryTake(out var lease) ? lease : CreateLease();
    }

    private JintEngineLease CreateLease()
    {
        var loader = new JintModuleLoader(_moduleCache);
        var engine = new Engine(options =>
        {
            // Convert exceptions thrown by host objects (e.g. `new URL('not-a-url')`, which a bundle wraps in
            // a try/catch to probe validity) into catchable JS errors. Jint otherwise bubbles them straight to
            // the CLR host, escaping the bundle's try/catch and aborting the whole render — ClearScript/V8
            // already surface host exceptions as JS errors, so this matches that behaviour.
            options
                .EnableModules(loader)
                .CatchClrExceptions();
        });

        engine.Execute(Shim.Source);

        return new JintEngineLease(engine, loader);
    }

    public void Return(JintEngineLease lease)
    {
        // The lease is owned exclusively by one wrapper between Rent and Return, so Uses is single-threaded.
        if (++lease.Uses >= _maxUsesPerEngine)
        {
            (lease.Engine as IDisposable)?.Dispose();
            return;
        }

        _leases.Add(lease);
    }

    public void Dispose()
    {
        while (_leases.TryTake(out var lease))
            (lease.Engine as IDisposable)?.Dispose();
    }
}
