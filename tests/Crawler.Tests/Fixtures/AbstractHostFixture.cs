using Crawler.AngleSharp;
using Crawler.AngleSharp.Js;
using Crawler.AngleSharp.Js.Jint;
using Crawler.AngleSharp.Js.Models;
using Crawler.AngleSharp.Js.V8;
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
        services.AddAngleSharpJintCrawler(options, renderOptions);
        services.AddAngleSharpV8Crawler(options, renderOptions);
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
        };
    }

    protected abstract IEnumerable<WebApplication> CreateHosts();

    protected virtual JsRenderOptions? CreateRenderOptions() => null;

    protected virtual List<string> GetLinks() => [];

    public AngleSharpJsCrawler<ScrapeResult> GetJsCrawler(JsEngine engine) => engine switch
    {
        JsEngine.Jint => ServiceProvider.GetRequiredService<DefaultAngleSharpJintCrawler>(),
        JsEngine.V8 => ServiceProvider.GetRequiredService<DefaultAngleSharpV8Crawler>(),
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
