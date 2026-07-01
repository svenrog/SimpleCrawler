using Crawler.Core;
using Crawler.Js.Abstractions;
using Crawler.Js.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crawler.Js.V8;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddV8Crawler(this IServiceCollection services, CrawlerOptions options, JsRenderOptions? renderOptions = null, V8EngineOptions? engineOptions = null, Action<IServiceProvider, HttpClient>? config = null)
    {
        services.AddJsCore(options, renderOptions, config);

        services.AddSingleton(Options.Create(engineOptions ?? new V8EngineOptions()));
        services.AddKeyedSingleton<IJsEngineFactory, V8JsEngineFactory>(DefaultV8Crawler.EngineKey);
        services.AddCrawlerHttpClient<DefaultV8Crawler>(config);
        services.AddTransient<ICrawler>(provider => provider.GetRequiredService<DefaultV8Crawler>());

        return services;
    }
}
