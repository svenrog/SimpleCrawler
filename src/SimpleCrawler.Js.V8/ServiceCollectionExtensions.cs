using Crawler.Core;
using Crawler.Js.Abstractions;
using Crawler.Js.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crawler.Js.V8;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddV8Crawler(this IServiceCollection services, CrawlerOptions options, Action<IServiceProvider, HttpClient>? config = null)
    {
        return AddV8Crawler(services, options, new JsRenderOptions(), new V8EngineOptions(), config);
    }

    public static IServiceCollection AddV8Crawler(this IServiceCollection services, CrawlerOptions options, JsRenderOptions renderOptions, Action<IServiceProvider, HttpClient>? config = null)
    {
        return AddV8Crawler(services, options, renderOptions, new V8EngineOptions(), config);
    }

    public static IServiceCollection AddV8Crawler(this IServiceCollection services, CrawlerOptions options, JsRenderOptions renderOptions, V8EngineOptions engineOptions, Action<IServiceProvider, HttpClient>? config = null)
    {
        services.AddJsCore(options, renderOptions, config);
        services.AddSingleton(Options.Create(engineOptions));
        services.AddKeyedSingleton<IJsEngineFactory, V8JsEngineFactory>(DefaultV8Crawler.EngineKey);
        services.AddCrawlerHttpClient<DefaultV8Crawler>(config);
        services.AddTransient<ICrawler>(provider => provider.GetRequiredService<DefaultV8Crawler>());

        return services;
    }
}
