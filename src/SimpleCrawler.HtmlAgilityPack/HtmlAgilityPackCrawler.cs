using SimpleCrawler.Core;
using SimpleCrawler.Core.Collectors;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Robots;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimpleCrawler.Core.Checkpoints;

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

    protected override (string? CanonicalHref, string? RobotsContent, IReadOnlyList<string?> LinkHrefs, PageSignals? Signals)
        ExtractStatic(HtmlDocument document)
    {
        var hrefs = new List<string?>();
        string? canonicalHref = null;
        string? robotsContent = null;
        var signals = CaptureSignals ? new PageSignals() : null;

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
            else if (name.Equals("meta", StringComparison.OrdinalIgnoreCase))
            {
                if (robotsContent is null && string.Equals(node.Attributes["name"]?.Value, "robots", StringComparison.OrdinalIgnoreCase))
                    robotsContent = node.Attributes["content"]?.Value;

                if (signals is not null)
                {
                    var metaName = node.Attributes["name"]?.Value ?? node.Attributes["property"]?.Value;
                    var content = node.Attributes["content"]?.Value;
                    if (metaName is not null && content is not null)
                        signals.MetaTags[metaName] = content;
                }
            }
            else if (signals is not null && name.Equals("script", StringComparison.OrdinalIgnoreCase))
            {
                var src = node.Attributes["src"]?.Value;
                if (!string.IsNullOrEmpty(src))
                {
                    signals.ScriptSources.Add(src);
                }
                else if (string.Equals(node.Attributes["type"]?.Value, "application/ld+json", StringComparison.OrdinalIgnoreCase))
                {
                    var jsonLd = node.InnerText.Trim();
                    if (jsonLd.Length > 0)
                        signals.JsonLdBlocks.Add(jsonLd);
                }
            }
        }

        return (canonicalHref, robotsContent, hrefs, signals);
    }
}
