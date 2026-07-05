using Crawler.Js.Abstractions;
using Crawler.Js.Models;
using Crawler.Core;
using Crawler.Core.Models;
using Crawler.Core.Robots;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.Js.Jint;

public sealed class DefaultJintCrawler : JsCrawler<ScrapeResult>, ICrawler
{
    internal const string EngineKey = "js-jint";

    public DefaultJintCrawler(HttpClient client, [FromKeyedServices(EngineKey)] IJsEngineFactory engineFactory, IRobotClient robotClient, IOptions<CrawlerOptions> options, IOptions<JsRenderOptions> renderOptions, ILogger<DefaultJintCrawler> logger)
        : base(client, engineFactory, robotClient, options, renderOptions, logger)
    {
    }

    protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new ScrapeResult { Urls = Visited });
    }
}
