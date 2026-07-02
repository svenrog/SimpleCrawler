using Crawler.Core;
using Crawler.Js.Abstractions;
using Crawler.Js.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Crawler.Js.Jint;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJintCrawler(this IServiceCollection services, CrawlerOptions options, Action<IServiceProvider, HttpClient>? config = null)
    {
        return AddJintCrawler(services, options, new JsRenderOptions(), config);
    }

    public static IServiceCollection AddJintCrawler(this IServiceCollection services, CrawlerOptions options, JsRenderOptions renderOptions, Action<IServiceProvider, HttpClient>? config = null)
    {
        services.AddJsCore(options, renderOptions, config);

        services.AddKeyedSingleton<IJsEngineFactory, JintJsEngineFactory>(DefaultJintCrawler.EngineKey);
        services.AddCrawlerHttpClient<DefaultJintCrawler>(config);
        services.AddTransient<ICrawler>(provider => provider.GetRequiredService<DefaultJintCrawler>());

        return services;
    }
}
