using Crawler.Core;
using Crawler.HtmlAgilityPack;
using Crawler.Tests.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.Tests.Common.Crawlers;

internal class HtmlAgilityPackCrawler : HtmlAgilityPackCrawler<ScrapeResult>
{
    public HtmlAgilityPackCrawler(HttpClient client, IOptions<CrawlerOptions> options, ILogger logger) : base(client, options, logger)
    {
    }

    protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new ScrapeResult { Urls = Visited });
    }
}
