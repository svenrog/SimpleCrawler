using Crawler.Core.Models;
using Crawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.Core;

public abstract class AbstractStaticHtmlCrawler<TResponse, TElement, TResult> : AbstractRobotsCrawler<TResponse, TResult>
    where TResult : IScrapeResult
{
    protected AbstractStaticHtmlCrawler(IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger logger) : base(robotClient, options, logger)
    {
    }

    protected override async ValueTask<PageExtract> ExtractPageData(TResponse response)
    {
        var canonicalUrl = await GetCanonical(response);
        var robots = await GetRobotsRules(response);

        var anchors = await CollectLinks(response);
        if (!anchors.TryGetNonEnumeratedCount(out var count))
            count = 0;

        var hrefs = new List<string?>(count);
        foreach (var anchor in anchors)
            hrefs.Add(await GetAttribute(anchor, "href"));

        return new PageExtract(canonicalUrl, robots, hrefs);
    }

    protected abstract ValueTask<IEnumerable<TElement>> CollectLinks(TResponse response);

    protected abstract ValueTask<string?> GetCanonical(TResponse response);

    protected abstract ValueTask<string?> GetAttribute(TElement element, string attributeName);

    protected abstract ValueTask<RobotsRules> GetRobotsRules(TResponse response);
}
