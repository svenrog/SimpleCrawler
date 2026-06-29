using Crawler.Js.Abstractions;
using Crawler.Js.Models;
using Crawler.Core;
using Crawler.Core.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crawler.Js.V8;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddV8Crawler(this IServiceCollection services, CrawlerOptions options, JsRenderOptions? renderOptions = null, Action<IServiceProvider, HttpClient>? config = null)
    {
        services.AddJsCore(options, renderOptions, config);

        services.AddKeyedSingleton<IJsEngineFactory, V8JsEngineFactory>(DefaultV8Crawler.EngineKey);

        services.AddHttpClient<DefaultV8Crawler>((provider, client) =>
        {
            config?.Invoke(provider, client);
            ConfigurationHelper.ConfigureClient(client, provider.GetRequiredService<IOptions<CrawlerOptions>>());
        }).ConfigurePrimaryHttpMessageHandler(provider =>
            ConfigurationHelper.CreatePrimaryHandler(provider.GetRequiredService<IOptions<CrawlerOptions>>()));

        return services;
    }
}
