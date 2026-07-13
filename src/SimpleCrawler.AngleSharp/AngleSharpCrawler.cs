using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimpleCrawler.Core;
using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Collectors;
using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Robots;

namespace SimpleCrawler.AngleSharp;

public abstract class AngleSharpCrawler<TResult> : AbstractStaticHtmlCrawler<IDocument, TResult>
    where TResult : IScrapeResult
{
    private static readonly HtmlParser _parser = new();

    protected AngleSharpCrawler(HttpClient client, IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger logger, ICheckpointStore? checkpoint = null, IEnumerable<ICrawlCollector>? collectors = null) : base(client, robotClient, options, logger, checkpoint, collectors)
    {
    }

    protected override IDocument ParseDocument(byte[] response)
    {
        using var stream = new MemoryStream(response, writable: false);
        return _parser.ParseDocument(stream);
    }

    protected override PageExtract ExtractStatic(IDocument document)
    {
        var anchors = document.QuerySelectorAll("a");

        var hrefs = new List<string?>(anchors.Length);
        foreach (var anchor in anchors)
            hrefs.Add(anchor.GetAttribute("href"));

        var canonicalHref = document.QuerySelector("link[rel='canonical']")?.GetAttribute("href");
        var robotsContent = document.QuerySelector("meta[name='robots']")?.GetAttribute("content");

        var robots = IndexingHelper.ParseMetaRobots(robotsContent);
        var dom = (IDomDispatch?)null;

        if (DomCollectors.Count > 0)
        {
            var page = new AngleSharpPageDom(document);
            dom = new StaticDomDispatch(page);
        }

        return new PageExtract(canonicalHref, robots, hrefs, dom);
    }

    protected override Task DisposeDocument(IDocument document)
    {
        document.Dispose();
        return Task.CompletedTask;
    }
}
