using SimpleCrawler.Js.Abstractions;
using SimpleCrawler.Js.Models;
using SimpleCrawler.Js.Rendering;
using SimpleCrawler.Core;
using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Extensions;
using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.Js;

public abstract class JsCrawler<TResult> : AbstractRobotsCrawler<JsExtract, JsExtract, TResult>
    where TResult : IScrapeResult
{
    private readonly HttpClient _client;
    private readonly JsRenderer _renderer;
    private readonly ILogger _logger;

    protected JsCrawler(HttpClient client, IJsEngineFactory engineFactory, IRobotClient robotClient, IOptions<CrawlerOptions> options, IOptions<JsRenderOptions> renderOptions, ILogger logger, ICheckpointStore? checkpoint = null)
        : base(robotClient, options, logger, checkpoint)
    {
        _client = client;
        _renderer = new JsRenderer(engineFactory, renderOptions.Value, logger);
        _logger = logger;
    }

    protected override async Task<JsExtract?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatus())
        {
            _logger.LogWarning("Error {code} on url '{url}'", response.StatusCode, url);
            return null;
        }

        _logger.LogDebug("Response '{code}' from url '{url}'", response.StatusCode, url);

        var shell = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return await _renderer.ExtractAsync(shell, url, _client, cancellationToken);
    }

    protected override ValueTask<JsExtract> ParseResponse(JsExtract response)
    {
        return new ValueTask<JsExtract>(response);
    }

    protected override ValueTask<PageExtract> ExtractPageData(JsExtract document)
    {
        var extract = new PageExtract(document.CanonicalHref, IndexingHelper.ParseMetaRobots(document.RobotsContent), document.LinkHrefs);
        return new ValueTask<PageExtract>(extract);
    }
}
