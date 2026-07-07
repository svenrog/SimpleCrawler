using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SimpleCrawler.AngleSharp;
using SimpleCrawler.Core;
using SimpleCrawler.Core.Models;
using SimpleCrawler.HtmlAgilityPack;
using SimpleCrawler.Js;
using SimpleCrawler.Js.Jint;
using SimpleCrawler.Js.Models;
using SimpleCrawler.Js.V8;
using SimpleCrawler.Playwright;
using SimpleCrawler.Puppeteer;
using SimpleCrawler.Tests.Common.Extensions;
using SimpleCrawler.Tests.Models;

namespace SimpleCrawler.Tests.Fixtures;

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
        var headlessOptions = CreateHeadlessOptions(options);

        var renderOptions = CreateRenderOptions();

        services.AddAngleSharpCrawler(options);
        services.AddJintCrawler(options, renderOptions ?? new());
        services.AddV8Crawler(options, renderOptions ?? new());
        services.AddHtmlAgilityPackCrawler(options);
        services.AddPlaywrightCrawler(headlessOptions);
        services.AddPuppeteerCrawler(headlessOptions);

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

    protected virtual HeadlessCrawlerOptions CreateHeadlessOptions(CrawlerOptions options)
    {
        return new HeadlessCrawlerOptions(options)
        {
            BlockNonEssentialResources = true,
            NetworkIdleGraceMs = 500,
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
