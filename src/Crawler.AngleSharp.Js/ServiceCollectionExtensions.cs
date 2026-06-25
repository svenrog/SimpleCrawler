using Crawler.Core;
using Crawler.Core.Helpers;
using Crawler.Core.Robots;
using Crawler.Core.Robots.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crawler.AngleSharp.Js;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAngleSharpJsCore(this IServiceCollection services, CrawlerOptions options, JsRenderOptions? renderOptions = null, Action<IServiceProvider, HttpClient>? config = null)
    {
        services.AddLogging();
        services.AddSingleton(Options.Create(options));
        services.AddSingleton(Options.Create(renderOptions ?? new JsRenderOptions()));

        services.AddHttpClient<IRobotClient, RobotWebClient>((provider, client) =>
        {
            config?.Invoke(provider, client);
            ConfigurationHelper.ConfigureClient(client, provider.GetRequiredService<IOptions<CrawlerOptions>>());
        }).ConfigurePrimaryHttpMessageHandler(provider =>
            ConfigurationHelper.CreatePrimaryHandler(provider.GetRequiredService<IOptions<CrawlerOptions>>()));

        return services;
    }
}
