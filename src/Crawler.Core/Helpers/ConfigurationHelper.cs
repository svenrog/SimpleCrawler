using Crawler.Core.Proxy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace Crawler.Core.Helpers;

public static class ConfigurationHelper
{
    public static void ConfigureClient(HttpClient client, CrawlerOptions options)
    {
        // Single-domain crawling benefits from HTTP/2 multiplexing; fall back to 1.1 where unsupported.
        client.DefaultRequestVersion = HttpVersion.Version20;
        client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;

        var profile = options.BrowserProfile;

        AddIfMissing(client, "User-Agent", profile.UserAgent);
        AddIfMissing(client, "Accept", profile.Accept);
        AddIfMissing(client, "Accept-Language", profile.AcceptLanguage);

        foreach (var header in profile.AdditionalHeaders)
            AddIfMissing(client, header.Key, header.Value);
    }

    private static void AddIfMissing(HttpClient client, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (client.DefaultRequestHeaders.Contains(name))
            return;

        client.DefaultRequestHeaders.Add(name, value);
    }

    public static void ConfigureClient(HttpClient client, IOptions<CrawlerOptions> options)
    {
        ConfigureClient(client, options.Value);
    }

    public static SocketsHttpHandler CreatePrimaryHandler(CrawlerOptions options)
    {
        return new SocketsHttpHandler
        {
            MaxConnectionsPerServer = options.EffectiveConcurrency,
            EnableMultipleHttp2Connections = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
            AutomaticDecompression = DecompressionMethods.All,
        };
    }

    public static SocketsHttpHandler CreatePrimaryHandler(IOptions<CrawlerOptions> options)
    {
        return CreatePrimaryHandler(options.Value);
    }

    public static HttpMessageHandler CreatePrimaryHandler(IServiceProvider provider)
    {
        var pool = provider.GetService<IProxyPool>();
        var options = provider.GetRequiredService<IOptions<CrawlerOptions>>().Value;

        if (pool is not null && options.ProxyPool is not null)
        {
            return new ProxyRoutingHandler(
                pool,
                provider.GetRequiredService<IProxyClientProvider>(),
                options.ProxyPool,
                provider.GetRequiredService<ILogger<ProxyRoutingHandler>>());
        }

        return CreatePrimaryHandler(options);
    }
}
