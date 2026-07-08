using Microsoft.Extensions.DependencyInjection;
using SimpleCrawler.Console.Checkpoints;
using SimpleCrawler.Console.Helpers;
using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Proxy;
using SimpleCrawler.HtmlAgilityPack;
using SystemConsole = System.Console;

namespace SimpleCrawler.Console.Extensions;

internal static class ServiceCollectionExtensions
{
    public static void AddCrawler(this IServiceCollection services, Options options)
    {
        services.AddSingleton(options);

        var proxyOptions = ConfigureProxyPool(services, options);
        var crawlerOptions = CrawlerOptionsMapper.Map(options, proxyOptions);

        ConfigureCheckpoint(services, options);

        services.AddHtmlAgilityPackCrawler(crawlerOptions);
    }

    private static void ConfigureCheckpoint(IServiceCollection services, Options options)
    {
        if (string.IsNullOrWhiteSpace(options.Checkpoint))
            return;

        services.AddSingleton<ICheckpointStore>(new JsonFileCheckpointStore(options.Checkpoint));
    }

    private static ProxyPoolOptions? ConfigureProxyPool(IServiceCollection services, Options options)
    {
        var manifest = ProxyCollector.Collect(options.Proxy);
        if (manifest.Length == 0)
            return null;

        SystemConsole.WriteLine($"Resolving {manifest.Length} proxy entries...");

        var resolver = new PreparedProxyResolver();
        var proxies = resolver.Resolve(manifest).Distinct().ToList();

        if (proxies.Count == 0)
        {
            SystemConsole.WriteLine("No usable proxies resolved; crawling without a proxy.");
            return null;
        }

        var poolOptions = new ProxyPoolOptions
        {
            Cooldown = TimeSpan.FromSeconds(options.ProxyCooldown),
            MinHealthyRatio = options.ProxyMinHealthy,
        };

        services.AddSingleton<IProxyPool>(_ => new ProxyPool(proxies, poolOptions));
        services.AddSingleton<IProxyClientProvider, ProxyHandlerProvider>();

        SystemConsole.WriteLine($"Proxy pool initialised with {proxies.Count} proxies.");

        return poolOptions;
    }
}
