using Crawler.Core;
using Crawler.Core.Models;
using Crawler.Core.Robots;
using JavaScriptEngineSwitcher.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.AngleSharp.Js.V8;

public sealed class DefaultAngleSharpV8Crawler : AngleSharpJsCrawler<ScrapeResult>
{
    internal const string SwitcherKey = "anglesharp-js-v8";

    public DefaultAngleSharpV8Crawler(HttpClient client, [FromKeyedServices(SwitcherKey)] IJsEngineSwitcher switcher, IRobotClient robotClient, IOptions<CrawlerOptions> options, IOptions<JsRenderOptions> renderOptions, ILogger<DefaultAngleSharpV8Crawler> logger)
        : base(client, switcher, robotClient, options, renderOptions, logger)
    {
    }

    protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new ScrapeResult { Urls = Visited });
    }
}
