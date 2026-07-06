using Crawler.Core;
using Crawler.Core.Helpers;
using Crawler.Core.Robots;
using Crawler.Core.Robots.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crawler.AngleSharp;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAngleSharpCrawler(this IServiceCollection services, CrawlerOptions options, Action<IServiceProvider, HttpClient>? config = null)
    {
        services.AddSingleton(Options.Create(options));
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
        }).ConfigurePrimaryHttpMessageHandler(provider =>
            ConfigurationHelper.CreatePrimaryHandler(provider));
        services.AddHttpClient<IRobotClient, RobotWebClient>((provider, client) =>
        {
            config?.Invoke(provider, client);
            ConfigurationHelper.ConfigureClient(client, provider.GetRequiredService<IOptions<CrawlerOptions>>());
        }).ConfigurePrimaryHttpMessageHandler(provider =>
            ConfigurationHelper.CreatePrimaryHandler(provider));
        services.AddTransient<ICrawler>(provider => provider.GetRequiredService<DefaultAngleSharpCrawler>());

        return services;
    }
}
