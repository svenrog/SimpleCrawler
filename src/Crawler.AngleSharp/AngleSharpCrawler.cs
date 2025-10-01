using AngleSharp;
using AngleSharp.Dom;
using Crawler.Core;
using Crawler.Core.Helpers;
using Crawler.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.AngleSharp;

public abstract class AngleSharpCrawler<TResult> : AbstractCrawler<IDocument, IElement, TResult>
    where TResult : IScrapeResult
{
    private readonly IConfiguration _configuration;

    protected AngleSharpCrawler(IConfiguration configuration, IOptions<CrawlerOptions> options, ILogger logger) : base(options, logger)
    {
        _configuration = configuration;
    }

    protected override async Task<IDocument?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        var context = BrowsingContext.New(_configuration);
        return await context.OpenAsync(url, cancellationToken);
    }

    protected override ValueTask<IEnumerable<IElement>> CollectLinks(IDocument response)
    {
        var result = response.QuerySelectorAll("a");
        return ValueTask.FromResult<IEnumerable<IElement>>(result);
    }

    protected override ValueTask<string?> GetCanonical(IDocument response)
    {
        var linkElement = response.QuerySelector("link[rel='canonical']");
        var href = linkElement?.Attributes["href"]?.Value;

        return ValueTask.FromResult(GetAbsoluteUrl(href));
    }

    protected override ValueTask<string?> GetAttribute(IElement element, string attributeName)
    {
        var attributeValue = element.Attributes[attributeName]?.Value;
        return ValueTask.FromResult(attributeValue);
    }

    protected override ValueTask<RobotsRules> GetRobotsRules(IDocument response)
    {
        var metaElement = response.QuerySelector("meta[name='robots']");
        var contentRuleValue = metaElement?.Attributes["content"]?.Value;

        return ValueTask.FromResult(IndexingHelper.ParseMetaRobots(contentRuleValue));
    }
}
