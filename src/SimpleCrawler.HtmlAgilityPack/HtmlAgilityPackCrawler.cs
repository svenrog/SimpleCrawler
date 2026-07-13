using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimpleCrawler.Core;
using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Collectors;
using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Robots;

namespace SimpleCrawler.HtmlAgilityPack;

public abstract class HtmlAgilityPackCrawler<TResult> : AbstractStaticHtmlCrawler<HtmlDocument, TResult>
    where TResult : IScrapeResult
{
    protected HtmlAgilityPackCrawler(HttpClient client, IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger logger, ICheckpointStore? checkpoint = null, IEnumerable<ICrawlCollector>? collectors = null) : base(client, robotClient, options, logger, checkpoint, collectors)
    {
    }

    protected override HtmlDocument ParseDocument(byte[] response)
    {
        var document = new HtmlDocument();
        using var stream = new MemoryStream(response, writable: false);
        document.Load(stream);

        return document;
    }

    protected override PageExtract ExtractStatic(HtmlDocument document)
    {
        var hrefs = new List<string?>();
        string? canonicalHref = null;
        string? robotsContent = null;

        var stack = new Stack<HtmlNode>();
        stack.Push(document.DocumentNode);

        while (stack.Count > 0)
        {
            var node = stack.Pop();

            var children = node.ChildNodes;
            for (var i = children.Count - 1; i >= 0; i--)
                stack.Push(children[i]);

            if (node.NodeType != HtmlNodeType.Element)
                continue;

            var name = node.Name;

            if (name.Equals("a", StringComparison.OrdinalIgnoreCase))
            {
                hrefs.Add(node.Attributes["href"]?.Value);
            }
            else if (canonicalHref is null && name.Equals("link", StringComparison.OrdinalIgnoreCase)
                && string.Equals(node.Attributes["rel"]?.Value, "canonical", StringComparison.OrdinalIgnoreCase))
            {
                canonicalHref = node.Attributes["href"]?.Value;
            }
            else if (robotsContent is null && name.Equals("meta", StringComparison.OrdinalIgnoreCase)
                && string.Equals(node.Attributes["name"]?.Value, "robots", StringComparison.OrdinalIgnoreCase))
            {
                robotsContent = node.Attributes["content"]?.Value;
            }
        }

        var robots = IndexingHelper.ParseMetaRobots(robotsContent);
        var dom = (IDomDispatch?)null;

        if (DomCollectors.Count > 0)
        {
            var page = new HtmlAgilityPackPageDom(document);
            dom = new StaticDomDispatch(page);
        }

        return new PageExtract(canonicalHref, robots, hrefs, dom);
    }
}
