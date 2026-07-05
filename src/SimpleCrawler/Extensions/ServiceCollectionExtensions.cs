using Crawler.Core;
using Crawler.Core.Browser;
using Crawler.Core.Proxy;
using Crawler.HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;
using SimpleCrawler.Helper;
using System.Net;

namespace SimpleCrawler.Extensions;

internal static class ServiceCollectionExtensions
{
    public static void AddCrawler(this IServiceCollection services, Options options)
    {
        services.AddSingleton(options);
        services.AddHtmlAgilityPackCrawler(MapCrawlerOptions(options), (provider, client) =>
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
            Proxy = MapProxy(options)
        };
    }

    private static IBrowserProfile MapBrowserProfile(Options options)
    {
        if (options.Impersonate == BrowserImpersonation.Chrome)
            return BrowserProfiles.Chrome;

        if (!string.IsNullOrEmpty(options.UserAgent))
            return new DefaultBrowserProfile { UserAgent = options.UserAgent };

        return BrowserProfiles.Default;
    }

    private static IWebProxy? MapProxy(Options options)
    {
        var proxyManifest = ProxyCollector.Collect(options.Proxy);
        if (proxyManifest.Length == 0)
            return null;

        Console.WriteLine($"Resolving {proxyManifest.Length} proxies...");

        var resolver = new PreparedProxyResolver();
        var proxies = resolver.Resolve(proxyManifest).Select(x => x.ToUri()).ToList();

        Console.WriteLine($"Proxy resolution done, resolved {proxies.Count} proxies.");

        if (proxies.Count > 1)
        {
            return new RoundRobinProxy(proxies);
        }
        else if (proxies.Count == 1)
        {
            return new WebProxy(proxies.First());
        }
        else
        {
            return null;
        }
    }

    private static void ConfigureHttpClient(HttpClient httpClient, Options options)
    {
        if (!string.IsNullOrEmpty(options.Cookie))
            httpClient.DefaultRequestHeaders.Add("Cookie", options.Cookie);
    }
}
