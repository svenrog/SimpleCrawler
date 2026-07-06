using Microsoft.Extensions.DependencyInjection;
using SimpleCrawler.Console.Helper;
using SimpleCrawler.Core;
using SimpleCrawler.Core.Browser;
using SimpleCrawler.Core.Proxy;
using SimpleCrawler.HtmlAgilityPack;
using SystemConsole = System.Console;

namespace SimpleCrawler.Console.Extensions;

internal static class ServiceCollectionExtensions
{
    public static void AddCrawler(this IServiceCollection services, Options options)
    {
        services.AddSingleton(options);

        var crawlerOptions = MapCrawlerOptions(options);
        ConfigureProxyPool(services, options, crawlerOptions);

        services.AddHtmlAgilityPackCrawler(crawlerOptions, (provider, client) =>
            ConfigureHttpClient(client, options));
    }

    private static CrawlerOptions MapCrawlerOptions(Options options)
    {
        return new CrawlerOptions
        {
            MaxPages = options.MaxPages,
            Concurrency = options.Concurrency,
            ParseConcurrency = options.ParseConcurrency,
            CrawlDelay = options.CrawlDelay,
            RespectMetaRobots = options.RespectRobots,
            RespectRobotsTxt = options.RespectRobots,
            BrowserProfile = MapBrowserProfile(options),
        };
    }

    private static void ConfigureProxyPool(IServiceCollection services, Options options, CrawlerOptions crawlerOptions)
    {
        var manifest = ProxyCollector.Collect(options.Proxy);
        if (manifest.Length == 0)
            return;

        SystemConsole.WriteLine($"Resolving {manifest.Length} proxy entries...");

        var resolver = new PreparedProxyResolver();
        var proxies = resolver.Resolve(manifest).Distinct().ToList();

        if (proxies.Count == 0)
        {
            SystemConsole.WriteLine("No usable proxies resolved; crawling without a proxy.");
            return;
        }

        var poolOptions = new ProxyPoolOptions
        {
            MaxRetries = options.ProxyRetries,
            Cooldown = TimeSpan.FromSeconds(options.ProxyCooldown),
            MinHealthyRatio = options.ProxyMinHealthy,
        };

        crawlerOptions.ProxyPool = poolOptions;
        services.AddSingleton<IProxyPool>(_ => new ProxyPool(proxies, poolOptions));
        services.AddSingleton<IProxyClientProvider, ProxyHandlerProvider>();

        SystemConsole.WriteLine($"Proxy pool initialised with {proxies.Count} proxies.");
    }

    private static IBrowserProfile MapBrowserProfile(Options options)
    {
        if (options.Impersonate == BrowserImpersonation.Chrome)
            return BrowserProfiles.Chrome;

        if (!string.IsNullOrEmpty(options.UserAgent))
            return new DefaultBrowserProfile { UserAgent = options.UserAgent };

        return BrowserProfiles.Default;
    }

    private static void ConfigureHttpClient(HttpClient httpClient, Options options)
    {
        if (!string.IsNullOrEmpty(options.Cookie))
            httpClient.DefaultRequestHeaders.Add("Cookie", options.Cookie);
    }
}
