using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using SimpleCrawler.Core;
using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.AngleSharp;

public abstract class AngleSharpCrawler<TResult> : AbstractStaticHtmlCrawler<IDocument, TResult>
    where TResult : IScrapeResult
{
    private static readonly HtmlParser _parser = new();

    protected AngleSharpCrawler(HttpClient client, IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger logger, ICheckpointStore? checkpoint = null) : base(client, robotClient, options, logger, checkpoint)
    {
    }

    protected override IDocument ParseDocument(byte[] response)
    {
        using var stream = new MemoryStream(response, writable: false);
        return _parser.ParseDocument(stream);
    }

    protected override (string? CanonicalHref, string? RobotsContent, IReadOnlyList<string?> LinkHrefs, PageSignals? Signals)
        ExtractStatic(IDocument document)
    {
        var anchors = document.QuerySelectorAll("a");

        var hrefs = new List<string?>(anchors.Length);
        foreach (var anchor in anchors)
            hrefs.Add(anchor.GetAttribute("href"));

        var canonicalHref = document.QuerySelector("link[rel='canonical']")?.GetAttribute("href");
        var robotsContent = document.QuerySelector("meta[name='robots']")?.GetAttribute("content");

        var signals = CaptureSignals ? ExtractSignals(document) : null;

        return (canonicalHref, robotsContent, hrefs, signals);
    }

    private static PageSignals ExtractSignals(IDocument document)
    {
        var signals = new PageSignals();

        foreach (var script in document.QuerySelectorAll("script"))
        {
            var src = script.GetAttribute("src");
            if (!string.IsNullOrEmpty(src))
            {
                signals.ScriptSources.Add(src);
            }
            else if (string.Equals(script.GetAttribute("type"), "application/ld+json", StringComparison.OrdinalIgnoreCase))
            {
                var jsonLd = script.TextContent.Trim();
                if (jsonLd.Length > 0)
                    signals.JsonLdBlocks.Add(jsonLd);
            }
        }

        foreach (var meta in document.QuerySelectorAll("meta"))
        {
            var name = meta.GetAttribute("name") ?? meta.GetAttribute("property");
            var content = meta.GetAttribute("content");
            if (name is not null && content is not null)
                signals.MetaTags[name] = content;
        }

        return signals;
    }

    protected override Task DisposeDocument(IDocument document)
    {
        document.Dispose();
        return Task.CompletedTask;
    }
}
