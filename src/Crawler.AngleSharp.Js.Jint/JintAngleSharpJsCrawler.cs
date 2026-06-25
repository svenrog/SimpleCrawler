using Crawler.Core;
using Crawler.Core.Models;
using Crawler.Core.Robots;
using JavaScriptEngineSwitcher.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.AngleSharp.Js.Jint;

public sealed class JintAngleSharpJsCrawler : AngleSharpJsCrawler<ScrapeResult>
{
    internal const string SwitcherKey = "anglesharp-js-jint";

    public JintAngleSharpJsCrawler(HttpClient client, [FromKeyedServices(SwitcherKey)] IJsEngineSwitcher switcher, IRobotClient robotClient, IOptions<CrawlerOptions> options, IOptions<JsRenderOptions> renderOptions, ILogger<JintAngleSharpJsCrawler> logger)
        : base(client, switcher, robotClient, options, renderOptions, logger)
    {
    }

    protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new ScrapeResult { Urls = Visited });
    }
}
