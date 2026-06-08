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

        if (string.IsNullOrEmpty(options.UserAgent))
            return;

        if (client.DefaultRequestHeaders.Contains("User-Agent"))
            return;

        client.DefaultRequestHeaders.Add("User-Agent", options.UserAgent);
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
}
