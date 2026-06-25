using Crawler.Core;
using Crawler.Core.Helpers;
using JavaScriptEngineSwitcher.Core;
using JavaScriptEngineSwitcher.Jint;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crawler.AngleSharp.Js.Jint;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAngleSharpJsJintCrawler(this IServiceCollection services, CrawlerOptions options, JsRenderOptions? renderOptions = null, Action<IServiceProvider, HttpClient>? config = null)
    {
        services.AddAngleSharpJsCore(options, renderOptions, config);

        services.AddKeyedSingleton<IJsEngineSwitcher>(DefaultAngleSharpJintCrawler.SwitcherKey, (_, _) =>
        {
            var switcher = new JsEngineSwitcher();
            switcher.EngineFactories.AddJint();
            switcher.DefaultEngineName = JintJsEngine.EngineName;
            return switcher;
        });

        services.AddHttpClient<DefaultAngleSharpJintCrawler>((provider, client) =>
        {
            config?.Invoke(provider, client);
            ConfigurationHelper.ConfigureClient(client, provider.GetRequiredService<IOptions<CrawlerOptions>>());
        }).ConfigurePrimaryHttpMessageHandler(provider =>
            ConfigurationHelper.CreatePrimaryHandler(provider.GetRequiredService<IOptions<CrawlerOptions>>()));

        return services;
    }
}
