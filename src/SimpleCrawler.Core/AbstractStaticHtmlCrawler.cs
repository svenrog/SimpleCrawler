using SimpleCrawler.Core.Collectors;
using SimpleCrawler.Core.Extensions;
using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimpleCrawler.Core.Checkpoints;

namespace SimpleCrawler.Core;

public abstract class AbstractStaticHtmlCrawler<TDocument, TResult> : AbstractRobotsCrawler<byte[], TDocument, TResult>
    where TResult : IScrapeResult
{
    private readonly HttpClient _client;
    private readonly ILogger _logger;
    private readonly long _maxResponseBodySize;

    protected AbstractStaticHtmlCrawler(HttpClient client, IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger logger, ICheckpointStore? checkpoint = null, IEnumerable<ICrawlCollector>? collectors = null) : base(robotClient, options, logger, checkpoint, collectors)
    {
        _client = client;
        _logger = logger;
        _maxResponseBodySize = options.Value.MaxResponseBodySize;
    }

    protected override async Task<byte[]?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(url, cancellationToken);

        var authority = new Uri(url).Authority;

        ReportResponse(url, HttpSignalCollector.ToResponseSignal(response, CaptureSignals));

        if (!response.IsSuccessStatus())
        {
            var status = (int)response.StatusCode;
            if (status is 429 or 503)
                Throttling.ReportRateLimited(authority, GetRetryAfter(response));

            _logger.LogWarning("Error '{code}' from '{url}'", (int)response.StatusCode, url);

            return null;
        }

        Throttling.ReportSuccess(authority);

        _logger.LogDebug("Response '{code}' from '{url}'", (int)response.StatusCode, url);

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
        var (canonicalHref, robotsContent, hrefs, signals) = ExtractStatic(document);

        var extract = new PageExtract(canonicalHref, IndexingHelper.ParseMetaRobots(robotsContent), hrefs, signals);
        return new ValueTask<PageExtract>(extract);
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null)
            return null;

        if (retryAfter.Delta is { } delta)
            return delta;

        if (retryAfter.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : null;
        }

        return null;
    }

    protected abstract TDocument ParseDocument(byte[] response);

    /// <summary>
    /// The returned <c>Signals</c> should only be populated (non-null) when <c>CaptureSignals</c> is
    /// true (i.e. a collector is registered), so crawls that don't opt in pay no extra per-page
    /// extraction cost.
    /// </summary>
    protected abstract (string? CanonicalHref, string? RobotsContent, IReadOnlyList<string?> LinkHrefs, PageSignals? Signals)
        ExtractStatic(TDocument document);
}
