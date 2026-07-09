using SimpleCrawler.Js.Abstractions;
using SimpleCrawler.Js.Models;
using SimpleCrawler.Core;
using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.Js.V8;

public sealed class DefaultV8Crawler : JsCrawler<ScrapeResult>, ICrawler
{
    internal const string EngineKey = "js-v8";

    public DefaultV8Crawler(HttpClient client, [FromKeyedServices(EngineKey)] IJsEngineFactory engineFactory, IRobotClient robotClient, IOptions<CrawlerOptions> options, IOptions<JsRenderOptions> renderOptions, ILogger<DefaultV8Crawler> logger, ICheckpointStore? checkpoint = null)
        : base(client, engineFactory, robotClient, options, renderOptions, logger, checkpoint)
    {
    }

    protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new ScrapeResult { Urls = Visited, Reports = Reports });
    }
}
