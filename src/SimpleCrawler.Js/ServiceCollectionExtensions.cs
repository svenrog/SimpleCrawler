using System.Diagnostics.CodeAnalysis;
using SimpleCrawler.Core;
using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Core.Collectors;
using SimpleCrawler.Core.Robots;
using SimpleCrawler.Core.Robots.Http;
using SimpleCrawler.Js.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.Js;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJsCore(this IServiceCollection services, CrawlerOptions options, JsRenderOptions? renderOptions = null, Action<IServiceProvider, HttpClient>? config = null)
    {
        services.AddLogging();
        services.AddSingleton(Options.Create(options));
        services.AddSingleton(Options.Create(renderOptions ?? new JsRenderOptions()));
        services.AddCrawlerCollectors(options);

        services.AddCrawlerHttpClient<IRobotClient, RobotWebClient>(config);

        return services;
    }

    public static IHttpClientBuilder AddCrawlerHttpClient<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TClient>(this IServiceCollection services, Action<IServiceProvider, HttpClient>? config)
        where TClient : class
    {
        return services.AddHttpClient<TClient>((provider, client) => ConfigureClient(provider, client, config))
            .ConfigurePrimaryHttpMessageHandler(ConfigurationHelper.CreatePrimaryHandler);
    }

    public static IHttpClientBuilder AddCrawlerHttpClient<TClient, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(this IServiceCollection services, Action<IServiceProvider, HttpClient>? config)
        where TClient : class
        where TImplementation : class, TClient
    {
        return services.AddHttpClient<TClient, TImplementation>((provider, client) => ConfigureClient(provider, client, config))
            .ConfigurePrimaryHttpMessageHandler(ConfigurationHelper.CreatePrimaryHandler);
    }

    private static void ConfigureClient(IServiceProvider provider, HttpClient client, Action<IServiceProvider, HttpClient>? config)
    {
        config?.Invoke(provider, client);
        ConfigurationHelper.ConfigureClient(client, provider.GetRequiredService<IOptions<CrawlerOptions>>());
    }
}
