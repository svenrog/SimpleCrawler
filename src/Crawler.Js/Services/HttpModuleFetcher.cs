using Crawler.Js.Abstractions;

namespace Crawler.Js.Services;

internal sealed class HttpModuleFetcher : IModuleFetcher
{
    private readonly HttpClient _client;
    private readonly SourceCache _cache;
    private readonly CancellationToken _cancellationToken;

    public HttpModuleFetcher(HttpClient client, SourceCache cache, CancellationToken cancellationToken)
    {
        _client = client;
        _cache = cache;
        _cancellationToken = cancellationToken;
    }

    public string? Fetch(Uri absolute)
    {
        if (_cache.TryGet(absolute, out var cached))
            return cached;

        return _cache.Store(absolute, Download(absolute));
    }

    // The engines resolve module imports synchronously, so we block on the synchronous
    // HttpClient.Send rather than risk sync-over-async deadlocks on GetAsync.
    //
    // A module specifier that resolves to a non-HTTP URI (a protocol-relative `//host/x.js` parses as a
    // file:// URI, and bundles also reference data:/blob: sources) or that fails to fetch must not abort the
    // whole page: the loader falls back to an empty module, so an unresolvable chunk degrades the render
    // instead of crashing it. Cancellation is the one failure that still propagates, so a stopped crawl stops.
    private string? Download(Uri absolute)
    {
        if (absolute.Scheme != Uri.UriSchemeHttp && absolute.Scheme != Uri.UriSchemeHttps)
            return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, absolute);
            using var response = _client.Send(request, _cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            using var stream = response.Content.ReadAsStream(_cancellationToken);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }
}
