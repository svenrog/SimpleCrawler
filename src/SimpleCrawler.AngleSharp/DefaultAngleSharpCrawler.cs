using SimpleCrawler.Core;
using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.AngleSharp;

public sealed class DefaultAngleSharpCrawler : AngleSharpCrawler<ScrapeResult>, ICrawler
{
    public DefaultAngleSharpCrawler(HttpClient client, IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger<DefaultAngleSharpCrawler> logger, ICheckpointStore? checkpoint = null) : base(client, robotClient, options, logger, checkpoint)
    {
    }

    protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new ScrapeResult { Urls = Visited, Reports = Reports });
    }
}
