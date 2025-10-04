namespace Crawler.Core.Helpers;

public static class ConfigurationHelper
{
    public static void ConfigureClient(HttpClient client, CrawlerOptions options)
    {
        if (string.IsNullOrEmpty(options.UserAgent))
            return;

        client.DefaultRequestHeaders.Add("User-Agent", options.UserAgent);
    }
}
