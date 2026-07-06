namespace Crawler.Js.Services;

// Every route of a client-only SPA returns the same shell pointing at the same bundle, so without a
// cross-page cache each crawled page re-fetches and re-materializes the identical module sources —
// often >85KB, landing straight on the LOH and driving Gen2. Keyed by absolute URL; bounded by an LRU
// cap so a large multi-bundle site can't retain every distinct chunk source for the whole crawl.
internal sealed class SourceCache
{
    private const int _capacity = 1024;

    private readonly BoundedLruCache<string, string?> _entries = new(_capacity);

    public bool TryGet(Uri url, out string? source) => _entries.TryGet(url.AbsoluteUri, out source);

    public string? Store(Uri url, string? source)
    {
        _entries.Set(url.AbsoluteUri, source);
        return source;
    }
}
