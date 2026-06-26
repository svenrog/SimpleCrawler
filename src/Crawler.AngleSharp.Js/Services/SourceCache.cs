using System.Collections.Concurrent;

namespace Crawler.AngleSharp.Js.Services;

// Every route of a client-only SPA returns the same shell pointing at the same bundle, so without a
// cross-page cache each crawled page re-fetches and re-materializes the identical module sources —
// often >85KB, landing straight on the LOH and driving Gen2. Keyed by absolute URL, lives one crawl.
internal sealed class SourceCache
{
    private readonly ConcurrentDictionary<string, string?> _entries = new(StringComparer.Ordinal);

    public bool TryGet(Uri url, out string? source) => _entries.TryGetValue(url.AbsoluteUri, out source);

    public string? Store(Uri url, string? source)
    {
        _entries[url.AbsoluteUri] = source;
        return source;
    }
}
