using Crawler.Core;
using Crawler.Core.Models;
using Crawler.Core.Robots;
using JavaScriptEngineSwitcher.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.AngleSharp.Js.V8;

public sealed class V8AngleSharpJsCrawler : AngleSharpJsCrawler<ScrapeResult>
{
    internal const string SwitcherKey = "anglesharp-js-v8";

    public V8AngleSharpJsCrawler(HttpClient client, [FromKeyedServices(SwitcherKey)] IJsEngineSwitcher switcher, IRobotClient robotClient, IOptions<CrawlerOptions> options, IOptions<JsRenderOptions> renderOptions, ILogger<V8AngleSharpJsCrawler> logger)
        : base(client, switcher, robotClient, options, renderOptions, logger)
    {
    }

    protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new ScrapeResult { Urls = Visited });
    }
}
