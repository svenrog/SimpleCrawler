using SimpleCrawler.Core;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.AngleSharp;

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
