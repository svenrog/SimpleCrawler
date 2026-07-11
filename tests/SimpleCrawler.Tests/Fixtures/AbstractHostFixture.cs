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
    private static readonly TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(10);

    public readonly ServiceProvider ServiceProvider;
    public readonly List<string> Links;

    private readonly IReadOnlyList<WebApplication> _hosts;
    private readonly CancellationTokenSource _stopping = new();

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

        ServiceProvider = services.BuildServiceProvider();

        _hosts = [.. CreateHosts()];
        foreach (var host in _hosts)
            host.StartAsync(CancellationToken.None).AwaitSync();

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
        // Bound the WHOLE shutdown, not just host StopAsync: host.DisposeAsync and - critically -
        // ServiceProvider.DisposeAsync (which tears down the Playwright/Puppeteer browser subprocesses)
        // are otherwise unbounded, so a wedged browser dispose would pin the test host long after the
        // runner (or the IDE "Stop" button) has given up on the run.
        _stopping.CancelAfter(_shutdownTimeout);

        try
        {
            await TeardownGuard.RunBounded(StopAndDisposeHosts, _shutdownTimeout);
        }
        finally
        {
            _stopping.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    private async Task StopAndDisposeHosts()
    {
        foreach (var host in _hosts)
        {
            try
            {
                await host.StopAsync(_stopping.Token);
            }
            catch
            {
                // Swallowed deliberately: the ServiceProvider owns the headless-browser subprocesses,
                // so a host that throws here must not skip it or the remaining hosts.
            }

            try
            {
                await host.DisposeAsync();
            }
            catch
            {
                // As above: keep going so ServiceProvider disposal is always reached.
            }
        }

        await ServiceProvider.DisposeAsync();
    }
}
