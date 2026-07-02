using Crawler.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crawler.Puppeteer;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPuppeteerCrawler(this IServiceCollection services, HeadlessCrawlerOptions options)
    {
        services.AddSingleton(Options.Create(options));
        services.AddPuppeteerCrawler();

        return services;
    }

    public static IServiceCollection AddPuppeteerCrawler(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddScoped<DefaultPuppeteerCrawler>();
        services.AddScoped<ICrawler>(provider => provider.GetRequiredService<DefaultPuppeteerCrawler>());
        return services;
    }
}
