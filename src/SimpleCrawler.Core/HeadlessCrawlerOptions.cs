using SimpleCrawler.Core.Retry;

namespace SimpleCrawler.Core;

public sealed class HeadlessCrawlerOptions : CrawlerOptions
{
    // A headless navigation is far costlier than an HTTP GET, so retrying a persistently-dead URL
    // several times hurts more than it helps; headless defaults to a smaller budget than static.
    private const int _defaultHeadlessMaxRetries = 1;

    public HeadlessCrawlerOptions()
    {
        Retry = new RetryOptions { MaxRetries = _defaultHeadlessMaxRetries };
    }

    public HeadlessCrawlerOptions(CrawlerOptions options)
    {
        Concurrency = options.Concurrency;
        ParseConcurrency = options.ParseConcurrency;
        MaxPages = options.MaxPages;
        CrawlDelay = options.CrawlDelay;
        RespectMetaRobots = options.RespectMetaRobots;
        RespectRobotsTxt = options.RespectRobotsTxt;
        EnableSitemapDiscovery = options.EnableSitemapDiscovery;
        BrowserProfile = options.BrowserProfile;
        ProxyPool = options.ProxyPool;
        Retry = options.Retry;
    }

    public bool BlockNonEssentialResources { get; set; } = true;

    /// <summary>
    /// Upper bound, in milliseconds, on the best-effort wait for network idle after a page loads.
    /// A page that never goes idle (constant analytics/tracking traffic) is extracted once this
    /// elapses rather than failing, so it caps per-page latency rather than guaranteeing idle.
    /// </summary>
    public int NetworkIdleGraceMs { get; set; } = 2000;
}
