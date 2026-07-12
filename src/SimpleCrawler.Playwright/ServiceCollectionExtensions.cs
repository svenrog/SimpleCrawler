using SimpleCrawler.Core;
using SimpleCrawler.Core.Collectors;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.Playwright;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPlaywrightCrawler(this IServiceCollection services, HeadlessCrawlerOptions options)
    {
        services.AddSingleton(Options.Create(options));
        services.AddCrawlerCollectors(options);
        services.AddPlaywrightCrawler();

        return services;
    }

    public static IServiceCollection AddPlaywrightCrawler(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddScoped<PlaywrightBrowserSession>();
        services.AddScoped<DefaultPlaywrightCrawler>();
        services.AddScoped<ICrawler>(provider => provider.GetRequiredService<DefaultPlaywrightCrawler>());
        services.AddScoped<IRobotClient, PlaywrightRobotClient>();

        return services;
    }
}
