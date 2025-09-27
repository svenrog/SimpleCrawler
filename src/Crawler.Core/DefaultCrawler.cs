using Crawler.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.Core;

public sealed class DefaultCrawler(HttpClient client, IOptions<CrawlerOptions> options, ILogger<DefaultCrawler> logger) : CrawlerBase<ScrapeResult>(client, options, logger)
{
    protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        var result = new ScrapeResult
        {
            Urls = [.. Visited]
        };

        return ValueTask.FromResult(result);
    }
}
