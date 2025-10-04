using Crawler.Core;
using Crawler.Core.Helpers;
using Crawler.Core.Models;
using Crawler.Core.Robots;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.HtmlAgilityPack;

public abstract class HtmlAgilityPackCrawler<TResult> : AbstractRobotsCrawler<HtmlDocument, HtmlNode, TResult>
    where TResult : IScrapeResult
{
    private readonly HttpClient _client;
    private readonly ILogger _logger;

    protected HtmlAgilityPackCrawler(HttpClient client, IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger logger) : base(robotClient, options, logger)
    {
        _client = client;
        _logger = logger;
    }

    protected override async Task<HtmlDocument?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        var response = await _client.GetAsync(url, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogDebug("Response '{code}' from url '{url}'", response.StatusCode, url);

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var document = new HtmlDocument();

            document.Load(stream);

            return document;
        }
        else
        {
            _logger.LogWarning("Error {code} on url '{url}'", response.StatusCode, url);

            return null;
        }
    }

    protected override ValueTask<IEnumerable<HtmlNode>> CollectLinks(HtmlDocument response)
    {
        var result = response.DocumentNode.SelectNodes("//a")
            ?? Enumerable.Empty<HtmlNode>();

        return ValueTask.FromResult(result);
    }

    protected override ValueTask<string?> GetCanonical(HtmlDocument response)
    {
        var linkElement = response.DocumentNode.SelectSingleNode("//link[@rel='canonical']");
        var href = linkElement?.Attributes["href"]?.Value;

        return ValueTask.FromResult(GetAbsoluteUrl(href));
    }

    protected override ValueTask<string?> GetAttribute(HtmlNode element, string attributeName)
    {
        var attributeValue = element.Attributes[attributeName]?.Value;
        return ValueTask.FromResult(attributeValue);
    }

    protected override ValueTask<RobotsRules> GetRobotsRules(HtmlDocument response)
    {
        var metaElement = response.DocumentNode.SelectSingleNode("//meta[@name='robots']");
        var contentRuleValue = metaElement?.Attributes["content"]?.Value;

        return ValueTask.FromResult(IndexingHelper.ParseMetaRobots(contentRuleValue));
    }
}
