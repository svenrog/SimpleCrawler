using SimpleCrawler.Core;
using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Collectors;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.AngleSharp;

public sealed class DefaultAngleSharpCrawler : AngleSharpCrawler<ScrapeResult>, ICrawler
{
    public DefaultAngleSharpCrawler(HttpClient client, IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger<DefaultAngleSharpCrawler> logger, ICheckpointStore? checkpoint = null, IEnumerable<ICrawlCollector>? collectors = null) : base(client, robotClient, options, logger, checkpoint, collectors)
    {
    }

    protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new ScrapeResult { Urls = Visited, Reports = Reports });
    }
}
