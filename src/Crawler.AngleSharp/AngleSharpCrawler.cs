using AngleSharp.Html.Parser;
using Crawler.Core;
using Crawler.Core.Models;
using Crawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.AngleSharp;

public abstract class AngleSharpCrawler<TResult> : AbstractStaticHtmlCrawler<TResult>
    where TResult : IScrapeResult
{
    private static readonly HtmlParser _parser = new();

    protected AngleSharpCrawler(HttpClient client, IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger logger) : base(client, robotClient, options, logger)
    {
    }

    protected override (string? CanonicalHref, string? RobotsContent, IReadOnlyList<string?> LinkHrefs) ExtractStatic(byte[] response)
    {
        using var stream = new MemoryStream(response, writable: false);
        using var document = _parser.ParseDocument(stream);

        var anchors = document.QuerySelectorAll("a");

        var hrefs = new List<string?>(anchors.Length);
        foreach (var anchor in anchors)
            hrefs.Add(anchor.GetAttribute("href"));

        var canonicalHref = document.QuerySelector("link[rel='canonical']")?.GetAttribute("href");
        var robotsContent = document.QuerySelector("meta[name='robots']")?.GetAttribute("content");

        return (canonicalHref, robotsContent, hrefs);
    }
}
