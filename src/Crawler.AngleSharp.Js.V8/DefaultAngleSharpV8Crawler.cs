using Crawler.AngleSharp.Js.Abstractions;
using Crawler.AngleSharp.Js.Models;
using Crawler.Core;
using Crawler.Core.Models;
using Crawler.Core.Robots;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.AngleSharp.Js.V8;

public sealed class DefaultAngleSharpV8Crawler : AngleSharpJsCrawler<ScrapeResult>
{
    internal const string EngineKey = "anglesharp-js-v8";

    public DefaultAngleSharpV8Crawler(HttpClient client, [FromKeyedServices(EngineKey)] ISpaEngineFactory engineFactory, IRobotClient robotClient, IOptions<CrawlerOptions> options, IOptions<JsRenderOptions> renderOptions, ILogger<DefaultAngleSharpV8Crawler> logger)
        : base(client, engineFactory, robotClient, options, renderOptions, logger)
    {
    }

    protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new ScrapeResult { Urls = Visited });
    }
}
