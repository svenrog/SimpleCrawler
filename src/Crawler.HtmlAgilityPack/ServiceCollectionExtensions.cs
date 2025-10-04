using Crawler.Core;
using Crawler.Core.Helpers;
using Crawler.Core.Robots;
using Crawler.Core.Robots.Http;
using Crawler.HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crawler.AngleSharp;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHtmlAgilityPackCrawler(this IServiceCollection services, CrawlerOptions options, Action<IServiceProvider, HttpClient>? config = null)
    {
        services.AddLogging();
        services.AddSingleton(Options.Create(options));
        services.AddHttpClient<DefaultHtmlAgilityPackCrawler>((provider, client) =>
        {
            ConfigurationHelper.ConfigureClient(client, options);
            config?.Invoke(provider, client);
        });
        services.AddHttpClient<IRobotClient, RobotWebClient>((provider, client) =>
        {
            ConfigurationHelper.ConfigureClient(client, options);
            config?.Invoke(provider, client);
        });
        return services;
    }
}
