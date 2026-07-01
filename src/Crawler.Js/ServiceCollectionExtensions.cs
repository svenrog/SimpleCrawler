using Crawler.Js.Models;
using Crawler.Core;
using Crawler.Core.Helpers;
using Crawler.Core.Robots;
using Crawler.Core.Robots.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crawler.Js;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJsCore(this IServiceCollection services, CrawlerOptions options, JsRenderOptions? renderOptions = null, Action<IServiceProvider, HttpClient>? config = null)
    {
        services.AddLogging();
        services.AddSingleton(Options.Create(options));
        services.AddSingleton(Options.Create(renderOptions ?? new JsRenderOptions()));

        services.AddCrawlerHttpClient<IRobotClient, RobotWebClient>(config);

        return services;
    }

    public static IHttpClientBuilder AddCrawlerHttpClient<TClient>(this IServiceCollection services, Action<IServiceProvider, HttpClient>? config)
        where TClient : class
    {
        return services.AddHttpClient<TClient>((provider, client) => ConfigureClient(provider, client, config))
            .ConfigurePrimaryHttpMessageHandler(provider =>
                ConfigurationHelper.CreatePrimaryHandler(provider.GetRequiredService<IOptions<CrawlerOptions>>()));
    }

    public static IHttpClientBuilder AddCrawlerHttpClient<TClient, TImplementation>(this IServiceCollection services, Action<IServiceProvider, HttpClient>? config)
        where TClient : class
        where TImplementation : class, TClient
    {
        return services.AddHttpClient<TClient, TImplementation>((provider, client) => ConfigureClient(provider, client, config))
            .ConfigurePrimaryHttpMessageHandler(provider =>
                ConfigurationHelper.CreatePrimaryHandler(provider.GetRequiredService<IOptions<CrawlerOptions>>()));
    }

    private static void ConfigureClient(IServiceProvider provider, HttpClient client, Action<IServiceProvider, HttpClient>? config)
    {
        config?.Invoke(provider, client);
        ConfigurationHelper.ConfigureClient(client, provider.GetRequiredService<IOptions<CrawlerOptions>>());
    }
}
