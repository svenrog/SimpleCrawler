using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SimpleCrawler.Core;
using SimpleCrawler.Core.Collectors;
using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Core.Robots;
using SimpleCrawler.Core.Robots.Http;

namespace SimpleCrawler.AngleSharp;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAngleSharpCrawler(this IServiceCollection services, CrawlerOptions options, Action<IServiceProvider, HttpClient>? config = null)
    {
        services.AddSingleton(Options.Create(options));
        services.AddCrawlCollectors(options);
        services.AddAngleSharpCrawler(config);

        return services;
    }

    public static IServiceCollection AddAngleSharpCrawler(this IServiceCollection services, Action<IServiceProvider, HttpClient>? config = null)
    {
        services.AddLogging();
        services.AddHttpClient<DefaultAngleSharpCrawler>((provider, client) =>
        {
            config?.Invoke(provider, client);
            ConfigurationHelper.ConfigureClient(client, provider.GetRequiredService<IOptions<CrawlerOptions>>());
        }).ConfigurePrimaryHttpMessageHandler(ConfigurationHelper.CreatePrimaryHandler);
        services.AddHttpClient<IRobotClient, RobotWebClient>((provider, client) =>
        {
            config?.Invoke(provider, client);
            ConfigurationHelper.ConfigureClient(client, provider.GetRequiredService<IOptions<CrawlerOptions>>());
        }).ConfigurePrimaryHttpMessageHandler(ConfigurationHelper.CreatePrimaryHandler);
        services.AddTransient<ICrawler>(provider => provider.GetRequiredService<DefaultAngleSharpCrawler>());

        return services;
    }
}
