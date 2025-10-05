using Crawler.Core;
using Crawler.Core.Helpers;
using Crawler.Core.Robots;
using Crawler.Core.Robots.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crawler.HtmlAgilityPack;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHtmlAgilityPackCrawler(this IServiceCollection services, CrawlerOptions options, Action<IServiceProvider, HttpClient>? config = null)
    {
        services.AddSingleton(Options.Create(options));
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
        });
        services.AddHttpClient<IRobotClient, RobotWebClient>((provider, client) =>
        {
            config?.Invoke(provider, client);
            ConfigurationHelper.ConfigureClient(client, provider.GetRequiredService<IOptions<CrawlerOptions>>());
        });
        return services;
    }
}
