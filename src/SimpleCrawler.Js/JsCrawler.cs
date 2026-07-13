using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimpleCrawler.Core;
using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Collectors;
using SimpleCrawler.Core.Extensions;
using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Robots;
using SimpleCrawler.Js.Abstractions;
using SimpleCrawler.Js.Models;
using SimpleCrawler.Js.Rendering;

namespace SimpleCrawler.Js;

public abstract class JsCrawler<TResult> : AbstractRobotsCrawler<JsExtract, JsExtract, TResult>
    where TResult : IScrapeResult
{
    private readonly HttpClient _client;
    private readonly JsRenderer _renderer;
    private readonly ILogger _logger;

    protected JsCrawler(HttpClient client, IJsEngineFactory engineFactory, IRobotClient robotClient, IOptions<CrawlerOptions> options, IOptions<JsRenderOptions> renderOptions, ILogger logger, ICheckpointStore? checkpoint = null, IEnumerable<ICrawlCollector>? collectors = null)
        : base(robotClient, options, logger, checkpoint, collectors)
    {
        var renderedCollectors = DomCollectors.OfType<IRenderedDomCollector>().ToArray();
        var collectorBlock = DomCollectors.Count > 0 ? DomScriptComposer.CollectorBlock(renderedCollectors) : null;

        _client = client;
        _renderer = new JsRenderer(engineFactory, renderOptions.Value, logger, collectorBlock);
        _logger = logger;
    }

    protected override async Task<JsExtract?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(url, cancellationToken);

        ReportResponse(url, HttpSignalCollector.ToResponseSignal(response, HasCollectors));

        if (!response.IsSuccessStatus())
        {
            _logger.LogWarning("Error '{code}' from '{url}'", (int)response.StatusCode, url);
            return null;
        }

        _logger.LogDebug("Response '{code}' from '{url}'", (int)response.StatusCode, url);

        var shell = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return await _renderer.ExtractAsync(shell, url, _client, cancellationToken);
    }

    protected override ValueTask<JsExtract> ParseResponse(JsExtract response)
    {
        return new ValueTask<JsExtract>(response);
    }

    protected override ValueTask<PageExtract> ExtractPageData(JsExtract document)
    {
        var dom = document.Collectors is { } collectors ? new RenderedDomDispatch(collectors) : null;
        var extract = new PageExtract(document.CanonicalHref, IndexingHelper.ParseMetaRobots(document.RobotsContent), document.LinkHrefs, dom);
        return new ValueTask<PageExtract>(extract);
    }
}
