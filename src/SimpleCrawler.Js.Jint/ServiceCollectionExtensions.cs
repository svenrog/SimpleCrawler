using SimpleCrawler.Core;
using SimpleCrawler.Js.Abstractions;
using SimpleCrawler.Js.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.Js.Jint;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Jint <see cref="IJsEngineFactory"/> (unkeyed) and its <see cref="JintEngineOptions"/>,
    /// and nothing else — no crawler, no robots client, no <see cref="CrawlerOptions"/>. The managed
    /// counterpart to <c>SimpleCrawler.Js.V8.ServiceCollectionExtensions.AddV8JsEngine</c>: a consumer that
    /// drives <see cref="Rendering.JsRenderer"/> directly swaps engines by swapping this one call, with no
    /// native dependency on this side.
    /// </summary>
    public static IServiceCollection AddJintJsEngine(this IServiceCollection services, JintEngineOptions? engineOptions = null)
    {
        services.AddSeededOptions(engineOptions);
        services.AddSingleton<IJsEngineFactory, JintJsEngineFactory>();

        return services;
    }

    public static IServiceCollection AddJintCrawler(this IServiceCollection services, CrawlerOptions options, Action<IServiceProvider, HttpClient>? config = null)
    {
        return AddJintCrawler(services, options, new JsRenderOptions(), new JintEngineOptions(), config);
    }

    public static IServiceCollection AddJintCrawler(this IServiceCollection services, CrawlerOptions options, JsRenderOptions renderOptions, Action<IServiceProvider, HttpClient>? config = null)
    {
        return AddJintCrawler(services, options, renderOptions, new JintEngineOptions(), config);
    }

    public static IServiceCollection AddJintCrawler(this IServiceCollection services, CrawlerOptions options, JsRenderOptions renderOptions, JintEngineOptions engineOptions, Action<IServiceProvider, HttpClient>? config = null)
    {
        services.AddJsCore(options, renderOptions, config);
        services.AddSeededOptions(engineOptions);
        services.AddKeyedSingleton<IJsEngineFactory, JintJsEngineFactory>(DefaultJintCrawler.EngineKey);
        services.AddCrawlerHttpClient<DefaultJintCrawler>(config);
        services.AddTransient<ICrawler>(provider => provider.GetRequiredService<DefaultJintCrawler>());

        return services;
    }
}
