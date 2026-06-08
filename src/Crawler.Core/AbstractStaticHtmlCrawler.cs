using Crawler.Core.Helpers;
using Crawler.Core.Models;
using Crawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.Core;

public abstract class AbstractStaticHtmlCrawler<TResponse, TResult> : AbstractRobotsCrawler<TResponse, TResult>
    where TResult : IScrapeResult
{
    protected AbstractStaticHtmlCrawler(IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger logger) : base(robotClient, options, logger)
    {
    }

    protected override ValueTask<PageExtract> ExtractPageData(TResponse response)
    {
        var (canonicalHref, robotsContent, hrefs) = ExtractStatic(response);

        var extract = new PageExtract(GetAbsoluteUrl(canonicalHref), IndexingHelper.ParseMetaRobots(robotsContent), hrefs);
        return new ValueTask<PageExtract>(extract);
    }

    protected abstract (string? CanonicalHref, string? RobotsContent, IReadOnlyList<string?> LinkHrefs) ExtractStatic(TResponse response);
}
