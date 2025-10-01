using Crawler.Benchmarks.Models;
using Crawler.Core;
using Crawler.HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Crawler.Benchmarks.Crawlers;

internal class HtmlAgilityPackCrawler : HtmlAgilityPackCrawler<NullScrapeResult>
{
    public HtmlAgilityPackCrawler(HttpClient client, IOptions<CrawlerOptions> options, ILogger logger) : base(client, options, logger)
    {
    }

    protected override ValueTask<NullScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new NullScrapeResult());
    }
}
