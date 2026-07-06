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

    protected AbstractStaticHtmlCrawler(HttpClient client, IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger logger) : base(robotClient, options, logger)
    {
        _client = client;
        _logger = logger;
    }

    protected override async Task<byte[]?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(url, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogDebug("Response '{code}' from url '{url}'", response.StatusCode, url);

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        else
        {
            _logger.LogWarning("Error {code} on url '{url}'", response.StatusCode, url);

            return null;
        }
    }

    protected override ValueTask<TDocument> ParseResponse(byte[] response)
    {
        return new ValueTask<TDocument>(ParseDocument(response));
    }

    protected override ValueTask<PageExtract> ExtractPageData(TDocument document)
    {
        var (canonicalHref, robotsContent, hrefs) = ExtractStatic(document);

        var extract = new PageExtract(GetAbsoluteUrl(canonicalHref), IndexingHelper.ParseMetaRobots(robotsContent), hrefs);
        return new ValueTask<PageExtract>(extract);
    }

    protected abstract TDocument ParseDocument(byte[] response);

    protected abstract (string? CanonicalHref, string? RobotsContent, IReadOnlyList<string?> LinkHrefs) ExtractStatic(TDocument document);
}
