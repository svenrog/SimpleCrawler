using Crawler.AngleSharp.Js.Abstractions;

namespace Crawler.AngleSharp.Js.Services;

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
    private string? Download(Uri absolute)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, absolute);
        using var response = _client.Send(request, _cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        using var stream = response.Content.ReadAsStream(_cancellationToken);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
