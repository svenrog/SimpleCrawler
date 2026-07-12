using SimpleCrawler.Core;
using SimpleCrawler.Core.Collectors;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimpleCrawler.Core.Checkpoints;

namespace SimpleCrawler.HtmlAgilityPack;

public sealed class DefaultHtmlAgilityPackCrawler : HtmlAgilityPackCrawler<ScrapeResult>, ICrawler
{
    public DefaultHtmlAgilityPackCrawler(HttpClient client, IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger<DefaultHtmlAgilityPackCrawler> logger, ICheckpointStore? checkpoint = null, IEnumerable<ICrawlCollector>? collectors = null) : base(client, robotClient, options, logger, checkpoint, collectors)
    {
    }

    protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new ScrapeResult { Urls = Visited, Reports = Reports });
    }
}