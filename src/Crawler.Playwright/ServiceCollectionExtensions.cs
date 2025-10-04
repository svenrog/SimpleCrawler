using Crawler.Core;
using Crawler.Playwright;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crawler.AngleSharp;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPlaywrightCrawler(this IServiceCollection services, CrawlerOptions options)
    {
        services.AddLogging();
        services.AddSingleton(Options.Create(options));
        services.AddScoped<DefaultPlaywrightCrawler>();
        return services;
    }
}
