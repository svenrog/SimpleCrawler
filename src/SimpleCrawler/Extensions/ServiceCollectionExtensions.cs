using Crawler.Core;
using Crawler.HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;
using ExtensionsOptions = Microsoft.Extensions.Options.Options;

namespace SimpleCrawler.Extensions;

internal static class ServiceCollectionExtensions
{
    public static void AddCrawler(this IServiceCollection services, Options options)
    {
        services.AddOptions(options);
        services.AddHtmlAgilityPackCrawler((provider, client) =>
            ConfigureHttpClient(client, options));
    }

    private static void AddOptions(this IServiceCollection services, Options options)
    {
        var crawlerOptions = MapCrawlerOptions(options);
        var optionsContainer = ExtensionsOptions.Create(crawlerOptions);

        services.AddSingleton(options);
        services.AddSingleton(optionsContainer);
    }

    private static CrawlerOptions MapCrawlerOptions(Options options)
    {
        return new CrawlerOptions
        {
            MaxPages = options.MaxPages,
            Parallelism = options.Parallelism,
            CrawlDelay = options.CrawlDelay,
            RespectMetaRobots = options.RespectRobots,
            RespectRobotsTxt = options.RespectRobots,
            UserAgent = options.UserAgent,
        };
    }

    private static void ConfigureHttpClient(HttpClient httpClient, Options options)
    {
        if (!string.IsNullOrEmpty(options.UserAgent))
            httpClient.DefaultRequestHeaders.Add("User-Agent", options.UserAgent);

        if (!string.IsNullOrEmpty(options.Cookie))
            httpClient.DefaultRequestHeaders.Add("Cookie", options.Cookie);
    }
}
