using AngleSharp;
using Crawler.AngleSharp;
using Crawler.Core;
using Crawler.Tests.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.Tests.Common.Crawlers;

internal class AngleSharpCrawler : AngleSharpCrawler<ScrapeResult>
{
    public AngleSharpCrawler(IConfiguration configuration, IOptions<CrawlerOptions> options, ILogger logger) : base(configuration, options, logger)
    {
    }

    protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new ScrapeResult { Urls = Visited });
    }
}
