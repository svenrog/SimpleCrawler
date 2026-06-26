using Crawler.AngleSharp.Js.Abstractions;
using Crawler.AngleSharp.Js.Models;
using Crawler.Core;
using Crawler.Core.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crawler.AngleSharp.Js.V8;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAngleSharpV8Crawler(this IServiceCollection services, CrawlerOptions options, JsRenderOptions? renderOptions = null, Action<IServiceProvider, HttpClient>? config = null)
    {
        services.AddAngleSharpJsCore(options, renderOptions, config);

        services.AddKeyedSingleton<IJsEngineFactory, V8JsEngineFactory>(DefaultAngleSharpV8Crawler.EngineKey);

        services.AddHttpClient<DefaultAngleSharpV8Crawler>((provider, client) =>
        {
            config?.Invoke(provider, client);
            ConfigurationHelper.ConfigureClient(client, provider.GetRequiredService<IOptions<CrawlerOptions>>());
        }).ConfigurePrimaryHttpMessageHandler(provider =>
            ConfigurationHelper.CreatePrimaryHandler(provider.GetRequiredService<IOptions<CrawlerOptions>>()));

        return services;
    }
}
