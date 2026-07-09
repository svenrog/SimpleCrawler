using Microsoft.Extensions.DependencyInjection;
using SimpleCrawler.Console.Checkpoints;
using SimpleCrawler.Console.Helpers;
using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Proxy;
#if HEADLESS
using SimpleCrawler.Core;
#endif
#if JS
using SimpleCrawler.Js.Models;
#endif
#if BACKEND_V8
using SimpleCrawler.Js.V8;
#elif BACKEND_ANGLESHARP
using SimpleCrawler.AngleSharp;
#elif BACKEND_JINT
using SimpleCrawler.Js.Jint;
#elif BACKEND_PLAYWRIGHT
using SimpleCrawler.Playwright;
#elif BACKEND_PUPPETEER
using SimpleCrawler.Puppeteer;
#else
using SimpleCrawler.HtmlAgilityPack;
#endif
using SystemConsole = System.Console;

namespace SimpleCrawler.Console.Extensions;

internal static class ServiceCollectionExtensions
{
    public static void AddCrawler(this IServiceCollection services, Options options)
    {
        services.AddSingleton(options);

        var proxyOptions = ConfigureProxyPool(services, options);
        var crawlerOptions = CrawlerOptionsMapper.Map(options, proxyOptions);

        ConfigureCheckpoint(services, options);

#if HEADLESS
        var headlessOptions = new HeadlessCrawlerOptions(crawlerOptions);
#endif

#if JS
        var renderOptions = new JsRenderOptions
        {
            EnableFetch = options.Fetch,
            EnableStreams = options.Streaming,
            EnableIndexedDb = options.IndexedDb,
        };
#endif

#if BACKEND_V8
        services.AddV8Crawler(crawlerOptions, renderOptions);
#elif BACKEND_JINT
        services.AddJintCrawler(crawlerOptions, renderOptions);
#elif BACKEND_ANGLESHARP
        services.AddAngleSharpCrawler(crawlerOptions);
#elif BACKEND_PLAYWRIGHT
        services.AddPlaywrightCrawler(headlessOptions);
#elif BACKEND_PUPPETEER
        services.AddPuppeteerCrawler(headlessOptions);
#else
        services.AddHtmlAgilityPackCrawler(crawlerOptions);
#endif
    }

    private static void ConfigureCheckpoint(IServiceCollection services, Options options)
    {
        if (string.IsNullOrWhiteSpace(options.Checkpoint))
            return;

        services.AddSingleton<ICheckpointStore>(new JsonFileCheckpointStore(options.Checkpoint));
    }

    private static ProxyPoolOptions? ConfigureProxyPool(IServiceCollection services, Options options)
    {
        var manifest = ProxyCollector.Collect(options.Proxy);
        if (manifest.Length == 0)
            return null;

        SystemConsole.WriteLine($"Resolving {manifest.Length} proxy entries...");

        var resolver = new PreparedProxyResolver();
        var proxies = resolver.Resolve(manifest).Distinct().ToList();

        if (proxies.Count == 0)
        {
            SystemConsole.WriteLine("No usable proxies resolved; crawling without a proxy.");
            return null;
        }

        var poolOptions = new ProxyPoolOptions
        {
            Cooldown = TimeSpan.FromSeconds(options.ProxyCooldown),
            MinHealthyRatio = options.ProxyMinHealthy,
        };

        services.AddSingleton<IProxyPool>(_ => new ProxyPool(proxies, poolOptions));
        services.AddSingleton<IProxyClientProvider, ProxyHandlerProvider>();

        SystemConsole.WriteLine($"Proxy pool initialised with {proxies.Count} proxies.");

        return poolOptions;
    }
}
