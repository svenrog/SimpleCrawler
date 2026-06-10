using Crawler.Core.Helpers;
using Crawler.Core.Models;
using Crawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.Core;

public abstract class AbstractStaticHtmlCrawler<TResult> : AbstractRobotsCrawler<byte[], TResult>
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

    protected override ValueTask<PageExtract> ExtractPageData(byte[] response)
    {
        var (canonicalHref, robotsContent, hrefs) = ExtractStatic(response);

        var extract = new PageExtract(GetAbsoluteUrl(canonicalHref), IndexingHelper.ParseMetaRobots(robotsContent), hrefs);
        return new ValueTask<PageExtract>(extract);
    }

    protected abstract (string? CanonicalHref, string? RobotsContent, IReadOnlyList<string?> LinkHrefs) ExtractStatic(byte[] response);
}
