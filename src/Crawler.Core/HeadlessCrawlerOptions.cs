namespace Crawler.Core;

public sealed class HeadlessCrawlerOptions : CrawlerOptions
{
    public HeadlessCrawlerOptions()
    {
    }

    public HeadlessCrawlerOptions(CrawlerOptions options)
    {
        UserAgent = options.UserAgent;
        Concurrency = options.Concurrency;
        ParseConcurrency = options.ParseConcurrency;
        MaxPages = options.MaxPages;
        CrawlDelay = options.CrawlDelay;
        RespectMetaRobots = options.RespectMetaRobots;
        RespectRobotsTxt = options.RespectRobotsTxt;
        EnableSitemapDiscovery = options.EnableSitemapDiscovery;
    }

    public bool BlockNonEssentialResources { get; set; } = true;

    /// <summary>
    /// Upper bound, in milliseconds, on the best-effort wait for network idle after a page loads.
    /// A page that never goes idle (constant analytics/tracking traffic) is extracted once this
    /// elapses rather than failing, so it caps per-page latency rather than guaranteeing idle.
    /// </summary>
    public int NetworkIdleGraceMs { get; set; } = 2000;
}
