using Crawler.Core;
using Crawler.Core.Browser;
using Crawler.HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;

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

    private static void ConfigureHttpClient(HttpClient httpClient, Options options)
    {
        if (!string.IsNullOrEmpty(options.Cookie))
            httpClient.DefaultRequestHeaders.Add("Cookie", options.Cookie);
    }
}
