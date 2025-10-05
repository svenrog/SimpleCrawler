using AngleSharp;
using Crawler.Core;
using Crawler.Core.Models;
using Crawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.AngleSharp;

public sealed class DefaultAngleSharpCrawler : AngleSharpCrawler<ScrapeResult>
{
    public DefaultAngleSharpCrawler(IConfiguration configuration, IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger<DefaultAngleSharpCrawler> logger) : base(configuration, robotClient, options, logger)
    {
    }

    protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new ScrapeResult { Urls = Visited });
    }
}
