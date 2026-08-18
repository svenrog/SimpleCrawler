using Microsoft.Extensions.Logging;
using SimpleCrawler.Js.Abstractions;
using SimpleCrawler.Core.Extensions;

namespace SimpleCrawler.Js.Services;

internal sealed class HttpModuleFetcher : IModuleFetcher
{
    private readonly HttpClient _client;
    private readonly SourceCache _cache;
    private readonly ILogger _logger;
    private readonly CancellationToken _cancellationToken;

    public HttpModuleFetcher(HttpClient client, SourceCache cache, ILogger logger, CancellationToken cancellationToken)
    {
        _client = client;
        _cache = cache;
        _logger = logger;
        _cancellationToken = cancellationToken;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Set once, after the document is parsed and before any script runs, because that is when the page's
    /// own map first exists.
    /// </remarks>
    public ImportMap? ImportMap { get; set; }

    public string? Fetch(Uri absolute)
    {
        if (_cache.TryGet(absolute, out var cached))
            return cached;

        return _cache.Store(absolute, Download(absolute));
    }

    /// <summary>
    /// The engines resolve module imports synchronously, so we block on the synchronous
    /// HttpClient.Send rather than risk sync-over-async deadlocks on GetAsync.
    ///
    /// A module specifier that resolves to a non-HTTP URI (a protocol-relative `//host/x.js` parses as a
    /// file:// URI, and bundles also reference data:/blob: sources) or that fails to fetch must not abort the
    /// whole page: the loader falls back to an empty module, so an unresolvable chunk degrades the render
    /// instead of crashing it. Cancellation is the one failure that still propagates, so a stopped crawl stops.
    /// <para>
    /// An empty module is a partial render, so a chunk this asked for and did not get is warned: what it would
    /// have registered is missing either way, and a consumer counting the renderer's warnings is how a caller
    /// tells a partial render from a page that never carried the code.
    /// </para>
    /// </summary>
    private string? Download(Uri absolute)
    {
        if (absolute.Scheme != Uri.UriSchemeHttp && absolute.Scheme != Uri.UriSchemeHttps)
            return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, absolute);
            using var response = _client.Send(request, _cancellationToken);
            if (!response.IsSuccessStatus())
            {
                _logger.LogWarning("Module source '{url}' was refused with status {status}.", absolute, (int)response.StatusCode);
                return null;
            }

            using var stream = response.Content.ReadAsStream(_cancellationToken);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("Module fetch error for '{url}': {message}", absolute, ex.Message);
            return null;
        }
    }
}
