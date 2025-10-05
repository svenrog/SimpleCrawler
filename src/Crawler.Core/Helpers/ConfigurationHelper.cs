using Microsoft.Extensions.Options;

namespace Crawler.Core.Helpers;

public static class ConfigurationHelper
{
    public static void ConfigureClient(HttpClient client, CrawlerOptions options)
    {
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
}
