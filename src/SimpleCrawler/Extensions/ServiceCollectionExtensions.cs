using Crawler.Core;
using Crawler.Js.HtmlAgilityPack;
using Crawler.Js.Models;
using Crawler.Js.V8;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SimpleCrawler.Extensions;

internal static class ServiceCollectionExtensions
{
    public static void AddCrawler(this IServiceCollection services, Options options)
    {
        var renderOptions = new JsRenderOptions
        {
            EnableFetch = true,
            ScriptLogging = LogLevel.Trace,
        };

        services.AddSingleton(options);
        services.AddHtmlAgilityPackHtmlParser();
        services.AddV8Crawler(MapCrawlerOptions(options), renderOptions, null, (provider, client) =>
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
