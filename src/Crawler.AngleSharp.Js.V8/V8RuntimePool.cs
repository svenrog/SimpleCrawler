using Microsoft.ClearScript.V8;
using System.Collections.Concurrent;

namespace Crawler.AngleSharp.Js.V8;

// A V8Runtime is a V8 isolate; creating one per page is the dominant per-page cost. Engines created
// from a shared runtime get isolated contexts (separate globals) but reuse the isolate's heap and
// compilation cache, so pooling runtimes amortizes isolate spin-up across pages. A runtime is
// single-threaded, so a rented runtime is held exclusively until the page's engine is disposed; the
// bag self-sizes to the crawl's peak concurrency.
internal sealed class V8RuntimePool : IDisposable
{
    private readonly ConcurrentBag<V8Runtime> _runtimes = new();

    public V8Runtime Rent()
    {
        return _runtimes.TryTake(out var runtime) ? runtime : new V8Runtime();
    }

    public void Return(V8Runtime runtime)
    {
        _runtimes.Add(runtime);
    }

    public void Dispose()
    {
        while (_runtimes.TryTake(out var runtime))
            runtime.Dispose();
    }
}
