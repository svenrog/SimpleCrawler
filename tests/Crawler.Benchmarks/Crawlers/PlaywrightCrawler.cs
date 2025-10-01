using Crawler.Benchmarks.Models;
using Crawler.Core;
using Crawler.Playwright;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;

namespace Crawler.Benchmarks.Crawlers;

internal class PlaywrightCrawler : PlaywrightCrawler<NullScrapeResult>
{
    public PlaywrightCrawler(IOptions<CrawlerOptions> options, ILogger logger) : base(options, logger)
    {
    }

    protected override ValueTask<NullScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new NullScrapeResult());
    }
}
