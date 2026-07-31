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
        services.AddCrawlCollectors(options);

        services.AddCrawlerHttpClient<IRobotClient, RobotWebClient>(config);

        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="TOptions"/> so a caller's own <c>Configure</c> composes with the
    /// <paramref name="seed"/> an <c>AddXyzEngine</c>/<c>AddXyzCrawler</c> overload was handed, or with the
    /// defaults when it was handed none. See <see cref="SeededOptionsFactory{TOptions}"/> for why the seed
    /// is not registered as the options value.
    /// </summary>
    public static IServiceCollection AddSeededOptions<TOptions>(this IServiceCollection services, TOptions? seed)
        where TOptions : class
    {
        services.AddOptions<TOptions>();
        if (seed is null)
        {
            return services;
        }

        services.AddSingleton<IOptionsFactory<TOptions>>(provider => new SeededOptionsFactory<TOptions>(
            seed,
            provider.GetServices<IConfigureOptions<TOptions>>(),
            provider.GetServices<IPostConfigureOptions<TOptions>>(),
            provider.GetServices<IValidateOptions<TOptions>>()));

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
