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
    private readonly SpaRenderer _renderer;

    protected AngleSharpJsCrawler(HttpClient client, ISpaEngineFactory engineFactory, IRobotClient robotClient, IOptions<CrawlerOptions> options, IOptions<JsRenderOptions> renderOptions, ILogger logger)
        : base(client, robotClient, options, logger)
    {
        _client = client;
        _renderer = new SpaRenderer(engineFactory, renderOptions.Value, logger);
    }

    protected override async Task<byte[]?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        var shell = await base.LoadResponse(url, cancellationToken);
        if (shell == null)
            return null;

        return await _renderer.RenderAsync(shell, url, _client, cancellationToken);
    }
}
