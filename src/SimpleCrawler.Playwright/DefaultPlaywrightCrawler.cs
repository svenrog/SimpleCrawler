using SimpleCrawler.Core;
using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.Playwright;

public sealed class DefaultPlaywrightCrawler : PlaywrightCrawler<ScrapeResult>, ICrawler
{
    public DefaultPlaywrightCrawler(IRobotClient robotClient, PlaywrightBrowserSession session, IOptions<HeadlessCrawlerOptions> options, ILogger<DefaultPlaywrightCrawler> logger, IProxyPool? pool = null, ICheckpointStore? checkpoint = null) : base(robotClient, session, options, logger, pool, checkpoint)
    {
    }

    protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new ScrapeResult { Urls = Visited, Reports = Reports });
    }
}
