using AngleSharp;
using Crawler.AngleSharp;
using Crawler.Benchmarks.Models;
using Crawler.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;

namespace Crawler.Benchmarks.Crawlers;

internal class AngleSharpCrawler : AngleSharpCrawler<NullScrapeResult>
{
    public AngleSharpCrawler(IConfiguration configuration, IOptions<CrawlerOptions> options, ILogger logger) : base(configuration, options, logger)
    {
    }

    protected override ValueTask<NullScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new NullScrapeResult());
    }
}
