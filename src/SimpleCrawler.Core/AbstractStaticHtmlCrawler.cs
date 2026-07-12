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
    private readonly bool _captureSignals;

    protected AbstractStaticHtmlCrawler(HttpClient client, IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger logger, ICheckpointStore? checkpoint = null) : base(robotClient, options, logger, checkpoint)
    {
        _client = client;
        _logger = logger;
        _maxResponseBodySize = options.Value.MaxResponseBodySize;
        _captureSignals = options.Value.CapturePageSignals;
    }

    /// <summary>Whether <see cref="ExtractStatic"/> should also collect DOM signals (scripts/meta/JSON-LD).</summary>
    protected bool CaptureSignals => _captureSignals;

    protected override async Task<byte[]?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(url, cancellationToken);

        var authority = new Uri(url).Authority;

        ReportResponse(url, (int)response.StatusCode, response.Content.Headers.ContentLength, response.Content.Headers.ContentType?.MediaType);

        if (_captureSignals)
            ReportSignals(url, CollectHeaders(response), CollectCookieNames(response));

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

    private static Dictionary<string, string> CollectHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers.Concat(response.Content.Headers))
            headers[header.Key.ToLowerInvariant()] = string.Join(", ", header.Value);

        return headers;
    }

    private static List<string> CollectCookieNames(HttpResponseMessage response)
    {
        var names = new List<string>();
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
            return names;

        foreach (var value in values)
        {
            var pair = value.Split(';', 2)[0];
            var equals = pair.IndexOf('=', StringComparison.Ordinal);
            if (equals > 0)
                names.Add(pair[..equals].Trim());
        }

        return names;
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
    /// The returned <c>Signals</c> should only be populated (non-null) when
    /// <see cref="CaptureSignals"/> is true, so crawls that don't opt in pay no extra per-page
    /// extraction cost.
    /// </summary>
    protected abstract (string? CanonicalHref, string? RobotsContent, IReadOnlyList<string?> LinkHrefs, PageSignals? Signals)
        ExtractStatic(TDocument document);
}
