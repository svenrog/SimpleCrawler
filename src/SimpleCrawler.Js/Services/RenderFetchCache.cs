using SimpleCrawler.Js.Network;

namespace SimpleCrawler.Js.Services;

// Client-only SPAs re-issue the same idempotent GET/HEAD fetches on every route — a shared header/footer
// whose nav prefetches each internal link, an RSC layout endpoint hit once per page. Without a cross-page
// cache each render re-fetches byte-identical responses over the network. Keyed by method + URL + request
// headers so only identical requests share an entry (RSC responses vary by routing headers), bounded by an
// LRU cap so a content-hashed multi-endpoint site can't retain every distinct response for the whole crawl.
internal sealed class RenderFetchCache
{
    private const int _capacity = 256;

    private readonly BoundedLruCache<string, JsHttpResponse> _entries = new(_capacity);

    public bool TryGet(string method, string url, string? headersJson, out JsHttpResponse response)
        => _entries.TryGet(Key(method, url, headersJson), out response);

    public void Store(string method, string url, string? headersJson, JsHttpResponse response)
        => _entries.Set(Key(method, url, headersJson), response);

    private static string Key(string method, string url, string? headersJson)
        => string.Concat(method, "\n", url, "\n", headersJson);
}
