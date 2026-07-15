using SimpleCrawler.Core;
using SimpleCrawler.Js.Abstractions;
using SimpleCrawler.Js.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.Js.V8;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the V8 <see cref="IJsEngineFactory"/> (unkeyed) and its <see cref="V8EngineOptions"/>, and
    /// nothing else — no crawler, no robots client, no <see cref="CrawlerOptions"/>.
    /// <para>
    /// This is the seam for a consumer that renders a page without crawling one: it drives
    /// <see cref="Rendering.JsRenderer"/> itself (see <c>JsRenderer.CollectAsync</c>) and needs an engine,
    /// not a pipeline. <c>AddV8Crawler</c> keys its own factory registration to the crawler that consumes
    /// it, so the two can coexist in one container; call this alone to avoid standing up a crawl pipeline
    /// just to obtain an engine.
    /// </para>
    /// </summary>
    public static IServiceCollection AddV8JsEngine(this IServiceCollection services, V8EngineOptions? engineOptions = null)
    {
        services.AddSingleton(Options.Create(engineOptions ?? new V8EngineOptions()));
        services.AddSingleton<IJsEngineFactory, V8JsEngineFactory>();

        return services;
    }

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
