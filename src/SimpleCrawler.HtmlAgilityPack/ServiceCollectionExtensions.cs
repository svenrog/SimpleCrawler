using SimpleCrawler.Core;
using SimpleCrawler.Core.Collectors;
using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Core.Robots;
using SimpleCrawler.Core.Robots.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.HtmlAgilityPack;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHtmlAgilityPackCrawler(this IServiceCollection services, CrawlerOptions options, Action<IServiceProvider, HttpClient>? config = null)
    {
        services.AddSingleton(Options.Create(options));
        services.AddCrawlerCollectors(options);
        services.AddHtmlAgilityPackCrawler(config);

        return services;
    }

    public static IServiceCollection AddHtmlAgilityPackCrawler(this IServiceCollection services, Action<IServiceProvider, HttpClient>? config = null)
    {
        services.AddLogging();
        services.AddHttpClient<DefaultHtmlAgilityPackCrawler>((provider, client) =>
        {
            config?.Invoke(provider, client);
            ConfigurationHelper.ConfigureClient(client, provider.GetRequiredService<IOptions<CrawlerOptions>>());
        }).ConfigurePrimaryHttpMessageHandler(ConfigurationHelper.CreatePrimaryHandler);
        services.AddHttpClient<IRobotClient, RobotWebClient>((provider, client) =>
        {
            config?.Invoke(provider, client);
            ConfigurationHelper.ConfigureClient(client, provider.GetRequiredService<IOptions<CrawlerOptions>>());
        }).ConfigurePrimaryHttpMessageHandler(ConfigurationHelper.CreatePrimaryHandler);
        services.AddTransient<ICrawler>(provider => provider.GetRequiredService<DefaultHtmlAgilityPackCrawler>());
        return services;
    }
}
