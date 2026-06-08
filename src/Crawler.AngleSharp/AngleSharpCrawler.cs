using AngleSharp;
using AngleSharp.Dom;
using Crawler.Core;
using Crawler.Core.Models;
using Crawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.AngleSharp;

public abstract class AngleSharpCrawler<TResult> : AbstractStaticHtmlCrawler<IDocument, TResult>
    where TResult : IScrapeResult
{
    private readonly IConfiguration _configuration;

    protected AngleSharpCrawler(IConfiguration configuration, IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger logger) : base(robotClient, options, logger)
    {
        _configuration = configuration;
    }

    protected override async Task<IDocument?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        var context = BrowsingContext.New(_configuration);
        return await context.OpenAsync(url, cancellationToken);
    }

    protected override (string? CanonicalHref, string? RobotsContent, IReadOnlyList<string?> LinkHrefs) ExtractStatic(IDocument response)
    {
        var anchors = response.QuerySelectorAll("a");

        var hrefs = new List<string?>(anchors.Length);
        foreach (var anchor in anchors)
            hrefs.Add(anchor.GetAttribute("href"));

        var canonicalHref = response.QuerySelector("link[rel='canonical']")?.GetAttribute("href");
        var robotsContent = response.QuerySelector("meta[name='robots']")?.GetAttribute("content");

        return (canonicalHref, robotsContent, hrefs);
    }
}
