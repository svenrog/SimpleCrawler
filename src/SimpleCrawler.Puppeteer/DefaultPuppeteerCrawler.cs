using SimpleCrawler.Core;
using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Collectors;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.Puppeteer;

public sealed class DefaultPuppeteerCrawler : PuppeteerCrawler<ScrapeResult>, ICrawler
{
    public DefaultPuppeteerCrawler(IRobotClient robotClient, PuppeteerBrowserSession session, IOptions<HeadlessCrawlerOptions> options, ILogger<DefaultPuppeteerCrawler> logger, IProxyPool? pool = null, ICheckpointStore? checkpoint = null, IEnumerable<ICrawlCollector>? collectors = null) : base(robotClient, session, options, logger, pool, checkpoint, collectors)
    {
    }

    protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new ScrapeResult { Urls = Visited, Reports = Reports });
    }
}
