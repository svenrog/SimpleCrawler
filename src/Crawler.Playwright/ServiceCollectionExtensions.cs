using Crawler.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crawler.Playwright;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPlaywrightCrawler(this IServiceCollection services, CrawlerOptions options)
    {
        services.AddSingleton(Options.Create(options));
        services.AddPlaywrightCrawler();

        return services;
    }

    public static IServiceCollection AddPlaywrightCrawler(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddScoped<DefaultPlaywrightCrawler>();
        return services;
    }
}
