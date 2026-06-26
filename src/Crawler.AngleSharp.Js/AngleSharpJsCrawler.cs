using Crawler.AngleSharp.Js.Abstractions;
using Crawler.AngleSharp.Js.Models;
using Crawler.AngleSharp.Js.Services;
using Crawler.Core;
using Crawler.Core.Models;
using Crawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.AngleSharp.Js;

public abstract class AngleSharpJsCrawler<TResult> : AngleSharpCrawler<TResult>
    where TResult : IScrapeResult
{
    private readonly HttpClient _client;
    private readonly JsRenderer _renderer;

    protected AngleSharpJsCrawler(HttpClient client, IJsEngineFactory engineFactory, IRobotClient robotClient, IOptions<CrawlerOptions> options, IOptions<JsRenderOptions> renderOptions, ILogger logger)
        : base(client, robotClient, options, logger)
    {
        _client = client;
        _renderer = new JsRenderer(engineFactory, renderOptions.Value, logger);
    }

    protected override async Task<byte[]?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        var shell = await base.LoadResponse(url, cancellationToken);
        if (shell == null)
            return null;

        return await _renderer.RenderAsync(shell, url, _client, cancellationToken);
    }
}
