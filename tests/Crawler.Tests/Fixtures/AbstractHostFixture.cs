using Crawler.AngleSharp;
using Crawler.Js;
using Crawler.Js.Jint;
using Crawler.Js.Models;
using Crawler.Js.V8;
using Crawler.Core;
using Crawler.Core.Models;
using Crawler.HtmlAgilityPack;
using Crawler.Playwright;
using Crawler.Puppeteer;
using Crawler.Tests.Common.Extensions;
using Crawler.Tests.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Crawler.Tests.Fixtures;

public abstract class AbstractHostFixture : IAsyncDisposable
{
    public readonly ServiceProvider ServiceProvider;
    public readonly CancellationTokenSource CancellationSource;
    public readonly List<string> Links;

    private readonly IReadOnlyList<WebApplication> _hosts;

    public AbstractHostFixture()
    {
        var services = new ServiceCollection();
        var options = CreateOptions();

        var renderOptions = CreateRenderOptions();

        services.AddAngleSharpCrawler(options);
        services.AddJintCrawler(options, renderOptions);
        services.AddV8Crawler(options, renderOptions);
        services.AddHtmlAgilityPackCrawler(options);
        services.AddPlaywrightCrawler(options);
        services.AddPuppeteerCrawler(options);

        services.AddSingleton<ILogger>(NullLogger.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddScoped<CancellationTokenSource>();

        ServiceProvider = services.BuildServiceProvider();
        CancellationSource = ServiceProvider.GetRequiredService<CancellationTokenSource>();

        _hosts = [.. CreateHosts()];
        foreach (var host in _hosts)
            host.StartAsync(CancellationSource.Token).AwaitSync();

        Links = GetLinks();
    }

    protected virtual CrawlerOptions CreateOptions()
    {
        return new CrawlerOptions
        {
            CrawlDelay = 0,
            Concurrency = 8,
            RespectMetaRobots = false,
            RespectRobotsTxt = false,
            EnableSitemapDiscovery = false,
        };
    }

    protected abstract IEnumerable<WebApplication> CreateHosts();

    protected virtual JsRenderOptions? CreateRenderOptions() => null;

    protected virtual List<string> GetLinks() => [];

    public JsCrawler<ScrapeResult> GetJsCrawler(JsEngine engine) => engine switch
    {
        JsEngine.Jint => ServiceProvider.GetRequiredService<DefaultJintCrawler>(),
        JsEngine.V8 => ServiceProvider.GetRequiredService<DefaultV8Crawler>(),
        _ => throw new ArgumentOutOfRangeException(nameof(engine)),
    };

    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
        {
            await host.StopAsync(CancellationSource.Token);
            await host.DisposeAsync();
        }

        await ServiceProvider.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}
