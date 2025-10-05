using AngleSharp;
using AngleSharp.Io.Network;
using Crawler.Core;
using Crawler.Core.Helpers;
using Crawler.Core.Robots;
using Crawler.Core.Robots.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Crawler.AngleSharp;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAngleSharpCrawler(this IServiceCollection services, CrawlerOptions options, Action<IServiceProvider, HttpClient>? config = null)
    {
        services.TryAddSingleton(Options.Create(options));
        services.AddAngleSharpCrawler(config);

        return services;
    }

    public static IServiceCollection AddAngleSharpCrawler(this IServiceCollection services, Action<IServiceProvider, HttpClient>? config = null)
    {
        services.AddLogging();
        services.TryAddScoped<DefaultAngleSharpCrawler>();
        services.AddHttpClient<HttpClientRequester>((provider, client) =>
        {
            ConfigurationHelper.ConfigureClient(client, provider.GetRequiredService<IOptions<CrawlerOptions>>());
            config?.Invoke(provider, client);
        });
        services.AddHttpClient<IRobotClient, RobotWebClient>((provider, client) =>
        {
            ConfigurationHelper.ConfigureClient(client, provider.GetRequiredService<IOptions<CrawlerOptions>>());
            config?.Invoke(provider, client);
        });
        services.TryAddSingleton((provider) =>
            Configuration.Default
                .WithRequester(provider.GetRequiredService<HttpClientRequester>())
                .WithDefaultLoader());

        return services;
    }
}
