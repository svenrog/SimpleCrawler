using Crawler.Js.Abstractions;
using Crawler.Js.Models;
using Crawler.Js.Parsing;
using Crawler.Js.Rendering;
using Crawler.Core;
using Crawler.Core.Helpers;
using Crawler.Core.Models;
using Crawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.Js;

public abstract class JsCrawler<TResult> : AbstractRobotsCrawler<JsExtract, TResult>
    where TResult : IScrapeResult
{
    private readonly HttpClient _client;
    private readonly JsRenderer _renderer;
    private readonly ILogger _logger;

    protected JsCrawler(HttpClient client, IJsEngineFactory engineFactory, IRobotClient robotClient, IOptions<CrawlerOptions> options, IOptions<JsRenderOptions> renderOptions, IEnumerable<IHtmlParser> parsers, ILogger logger)
        : base(robotClient, options, logger)
    {
        _client = client;
        _renderer = new JsRenderer(engineFactory, renderOptions.Value, parsers.FirstOrDefault(), logger);
        _logger = logger;
    }

    protected override async Task<JsExtract?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Error {code} on url '{url}'", response.StatusCode, url);
            return null;
        }

        _logger.LogDebug("Response '{code}' from url '{url}'", response.StatusCode, url);

        var shell = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return await _renderer.ExtractAsync(shell, url, _client, cancellationToken);
    }

    protected override ValueTask<PageExtract> ExtractPageData(JsExtract response)
    {
        var extract = new PageExtract(GetAbsoluteUrl(response.CanonicalHref), IndexingHelper.ParseMetaRobots(response.RobotsContent), response.LinkHrefs);
        return new ValueTask<PageExtract>(extract);
    }
}
