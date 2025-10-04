using Crawler.Alleima.ETrack;
using Crawler.Core;
using Crawler.Core.Robots;
using Crawler.Core.Robots.Http;
using Microsoft.Extensions.DependencyInjection;
using ExtensionsOptions = Microsoft.Extensions.Options.Options;

namespace SimpleCrawler.Extensions;

internal static class ServiceCollectionExtensions
{
    public static void AddCrawler(this IServiceCollection services, Options options)
    {
        services.AddOptions(options);
        services.AddHttpClient<AlleimaCrawler>((provider, client) =>
        {
            var options = provider.GetRequiredService<Options>();
            if (options.Cookie != null)
            {
                client.DefaultRequestHeaders.Add("Cookie", options.Cookie);
            }
        });
        services.AddHttpClient<IRobotClient, RobotWebClient>();
    }

    private static void AddOptions(this IServiceCollection services, Options options)
    {
        var crawlerOptions = Map(options);
        var optionsContainer = ExtensionsOptions.Create(crawlerOptions);

        services.AddSingleton(options);
        services.AddSingleton(optionsContainer);
    }

    private static CrawlerOptions Map(Options options)
    {
        return new CrawlerOptions
        {
            MaxPages = options.MaxPages,
            Parallelism = options.Parallelism,
            CrawlDelay = options.CrawlDelay,
            RespectMetaRobots = options.RespectMetaRobots,
        };
    }
}
