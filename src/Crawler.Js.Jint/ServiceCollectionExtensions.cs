using Crawler.Js.Abstractions;
using Crawler.Js.Models;
using Crawler.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Crawler.Js.Jint;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJintCrawler(this IServiceCollection services, CrawlerOptions options, JsRenderOptions? renderOptions = null, Action<IServiceProvider, HttpClient>? config = null)
    {
        services.AddJsCore(options, renderOptions, config);

        services.AddKeyedSingleton<IJsEngineFactory, JintJsEngineFactory>(DefaultJintCrawler.EngineKey);
        services.AddCrawlerHttpClient<DefaultJintCrawler>(config);
        services.AddTransient<ICrawler>(provider => provider.GetRequiredService<DefaultJintCrawler>());

        return services;
    }
}
