using Microsoft.ClearScript.V8;
using System.Collections.Concurrent;

namespace Crawler.Js.V8;

// A V8Runtime is a V8 isolate; creating one per page is the dominant per-page cost. Engines created
// from a shared runtime get isolated contexts (separate globals) but reuse the isolate's heap and
// compilation cache, so pooling runtimes amortizes isolate spin-up across pages. A runtime is
// single-threaded, so a rented runtime is held exclusively until the page's engine is disposed; the
// bag self-sizes to the crawl's peak concurrency.
//
// A V8 isolate never hands heap back to the OS and its compilation cache grows with every distinct
// script it runs, so on a heterogeneous site a long-lived pooled isolate balloons unbounded. Each
// runtime is therefore retired after a fixed number of pages and replaced with a fresh one, releasing
// the accumulated heap and compilation cache while still amortizing spin-up over many pages.
internal sealed class V8RuntimePool : IDisposable
{
    private readonly ConcurrentBag<V8RuntimeLease> _leases = [];
    private readonly uint _maxHeapSizeBytes;
    private readonly uint _maxUsesPerRuntime;
    private readonly TimeSpan _heapSampleInterval;

    public V8RuntimePool(V8EngineOptions options)
    {
        _maxHeapSizeBytes = options.MaxHeapSizeMb > 0 ? (uint)options.MaxHeapSizeMb * 1024 * 1024 : 0;
        _maxUsesPerRuntime = options.MaxUsesPerRuntime > 0 ? (uint)options.MaxUsesPerRuntime : 0;
        _heapSampleInterval = options.HeapSampleInterval;
    }

    public V8RuntimeLease Rent()
    {
        return _leases.TryTake(out var lease) ? lease : new V8RuntimeLease(CreateRuntime());
    }

    // A soft, sampled heap cap: ClearScript interrupts and throws a catchable error once the isolate
    // crosses MaxHeapSize, so a runaway page is aborted (and caught per-page) instead of taking the
    // process down — unlike V8RuntimeConstraints, whose hard limit raises a fatal OOM.
    private V8Runtime CreateRuntime()
    {
        var runtime = new V8Runtime();
        if (_maxHeapSizeBytes > 0)
        {
            runtime.MaxHeapSize = _maxHeapSizeBytes;
            runtime.HeapSizeSampleInterval = _heapSampleInterval;
        }

        return runtime;
    }

    public void Return(V8RuntimeLease lease)
    {
        // The lease is owned exclusively by one engine between Rent and Return, so Uses is single-threaded.
        if (++lease.Uses >= _maxUsesPerRuntime)
        {
            lease.Runtime.Dispose();
            return;
        }

        _leases.Add(lease);
    }

    public void Dispose()
    {
        while (_leases.TryTake(out var lease))
            lease.Runtime.Dispose();
    }
}
