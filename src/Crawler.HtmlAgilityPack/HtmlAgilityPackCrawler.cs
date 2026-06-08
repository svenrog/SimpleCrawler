using Crawler.Core;
using Crawler.Core.Models;
using Crawler.Core.Robots;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.HtmlAgilityPack;

public abstract class HtmlAgilityPackCrawler<TResult> : AbstractStaticHtmlCrawler<HtmlDocument, TResult>
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

    protected override (string? CanonicalHref, string? RobotsContent, IReadOnlyList<string?> LinkHrefs) ExtractStatic(HtmlDocument response)
    {
        var hrefs = new List<string?>();
        string? canonicalHref = null;
        string? robotsContent = null;

        var stack = new Stack<HtmlNode>();
        stack.Push(response.DocumentNode);

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
                hrefs.Add(node.GetAttributeValue<string?>("href", null));
            }
            else if (canonicalHref is null && name.Equals("link", StringComparison.OrdinalIgnoreCase)
                && string.Equals(node.GetAttributeValue<string?>("rel", null), "canonical", StringComparison.OrdinalIgnoreCase))
            {
                canonicalHref = node.GetAttributeValue<string?>("href", null);
            }
            else if (robotsContent is null && name.Equals("meta", StringComparison.OrdinalIgnoreCase)
                && string.Equals(node.GetAttributeValue<string?>("name", null), "robots", StringComparison.OrdinalIgnoreCase))
            {
                robotsContent = node.GetAttributeValue<string?>("content", null);
            }
        }

        return (canonicalHref, robotsContent, hrefs);
    }
}
