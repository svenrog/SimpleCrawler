using Crawler.Core;
using Crawler.Core.Models;
using Crawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.AngleSharp;

public sealed class DefaultAngleSharpCrawler : AngleSharpCrawler<ScrapeResult>, ICrawler
{
    public DefaultAngleSharpCrawler(HttpClient client, IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger<DefaultAngleSharpCrawler> logger) : base(client, robotClient, options, logger)
    {
    }

    protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new ScrapeResult { Urls = Visited });
    }
}
