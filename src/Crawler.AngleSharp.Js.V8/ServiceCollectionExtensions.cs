using Crawler.Core;
using Crawler.Core.Helpers;
using JavaScriptEngineSwitcher.Core;
using JavaScriptEngineSwitcher.V8;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crawler.AngleSharp.Js.V8;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAngleSharpJsV8Crawler(this IServiceCollection services, CrawlerOptions options, JsRenderOptions? renderOptions = null, Action<IServiceProvider, HttpClient>? config = null)
    {
        services.AddAngleSharpJsCore(options, renderOptions, config);

        services.AddKeyedSingleton<IJsEngineSwitcher>(DefaultAngleSharpV8Crawler.SwitcherKey, (_, _) =>
        {
            var switcher = new JsEngineSwitcher();
            switcher.EngineFactories.AddV8();
            switcher.DefaultEngineName = V8JsEngine.EngineName;
            return switcher;
        });

        services.AddHttpClient<DefaultAngleSharpV8Crawler>((provider, client) =>
        {
            config?.Invoke(provider, client);
            ConfigurationHelper.ConfigureClient(client, provider.GetRequiredService<IOptions<CrawlerOptions>>());
        }).ConfigurePrimaryHttpMessageHandler(provider =>
            ConfigurationHelper.CreatePrimaryHandler(provider.GetRequiredService<IOptions<CrawlerOptions>>()));

        return services;
    }
}
