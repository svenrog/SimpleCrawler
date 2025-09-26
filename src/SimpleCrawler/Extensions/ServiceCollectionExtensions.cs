using Crawler.Core;
using Microsoft.Extensions.DependencyInjection;
using ExtensionsOptions = Microsoft.Extensions.Options.Options;

namespace SimpleCrawler.Extensions;

internal static class ServiceCollectionExtensions
{
    public static void AddOptions(this IServiceCollection services, Options options)
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
            Entry = options.Entry,
            MaxPages = options.MaxPages,
            Parallellism = options.Parallellism
        };
    }
}
