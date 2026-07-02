using Crawler.Core;
using Crawler.Core.Models;
using Crawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.Puppeteer;

public sealed class DefaultPuppeteerCrawler : PuppeteerCrawler<ScrapeResult>, ICrawler
{
    public DefaultPuppeteerCrawler(IRobotClient robotClient, IOptions<HeadlessCrawlerOptions> options, ILogger logger) : base(robotClient, options, logger)
    {
    }

    protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new ScrapeResult { Urls = Visited });
    }
}
