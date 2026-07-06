using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using SimpleCrawler.Core;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.AngleSharp;

public abstract class AngleSharpCrawler<TResult> : AbstractStaticHtmlCrawler<IDocument, TResult>
    where TResult : IScrapeResult
{
    private static readonly HtmlParser _parser = new();

    protected AngleSharpCrawler(HttpClient client, IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger logger) : base(client, robotClient, options, logger)
    {
    }

    protected override IDocument ParseDocument(byte[] response)
    {
        using var stream = new MemoryStream(response, writable: false);
        return _parser.ParseDocument(stream);
    }

    protected override (string? CanonicalHref, string? RobotsContent, IReadOnlyList<string?> LinkHrefs) ExtractStatic(IDocument document)
    {
        var anchors = document.QuerySelectorAll("a");

        var hrefs = new List<string?>(anchors.Length);
        foreach (var anchor in anchors)
            hrefs.Add(anchor.GetAttribute("href"));

        var canonicalHref = document.QuerySelector("link[rel='canonical']")?.GetAttribute("href");
        var robotsContent = document.QuerySelector("meta[name='robots']")?.GetAttribute("content");

        return (canonicalHref, robotsContent, hrefs);
    }

    protected override Task DisposeDocument(IDocument document)
    {
        document.Dispose();
        return Task.CompletedTask;
    }
}
