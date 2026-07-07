using SimpleCrawler.Core.Extensions;
using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.Core;

public abstract class AbstractStaticHtmlCrawler<TDocument, TResult> : AbstractRobotsCrawler<byte[], TDocument, TResult>
    where TResult : IScrapeResult
{
    private readonly HttpClient _client;
    private readonly ILogger _logger;
    private readonly long _maxResponseBodySize;

    protected AbstractStaticHtmlCrawler(HttpClient client, IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger logger) : base(robotClient, options, logger)
    {
        _client = client;
        _logger = logger;
        _maxResponseBodySize = options.Value.MaxResponseBodySize;
    }

    protected override async Task<byte[]?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Error {code} on url '{url}'", response.StatusCode, url);

            return null;
        }

        _logger.LogDebug("Response '{code}' from url '{url}'", response.StatusCode, url);

        if (_maxResponseBodySize <= 0)
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);

        var body = await response.Content.ReadCappedByteArrayAsync(_maxResponseBodySize, cancellationToken);
        if (body is null)
            _logger.LogWarning("Response body from url '{url}' exceeded the {limit}-byte cap; skipping.", url, _maxResponseBodySize);

        return body;
    }

    protected override ValueTask<TDocument> ParseResponse(byte[] response)
    {
        return new ValueTask<TDocument>(ParseDocument(response));
    }

    protected override ValueTask<PageExtract> ExtractPageData(TDocument document)
    {
        var (canonicalHref, robotsContent, hrefs) = ExtractStatic(document);

        var extract = new PageExtract(canonicalHref, IndexingHelper.ParseMetaRobots(robotsContent), hrefs);
        return new ValueTask<PageExtract>(extract);
    }

    protected abstract TDocument ParseDocument(byte[] response);

    protected abstract (string? CanonicalHref, string? RobotsContent, IReadOnlyList<string?> LinkHrefs) ExtractStatic(TDocument document);
}
