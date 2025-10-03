using Crawler.Core;
using Crawler.Playwright;
using Crawler.Tests.Common.Extensions;
using Crawler.Tests.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.Tests.Common.Crawlers;

internal class PlaywrightCrawler : PlaywrightCrawler<ScrapeResult>, IDisposable
{
    public PlaywrightCrawler(IOptions<CrawlerOptions> options, ILogger logger) : base(options, logger)
    {
    }

    public void Dispose()
    {
        this.DisposeAsync().AwaitSync();
    }

    protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new ScrapeResult { Urls = Visited });
    }
}
