using SimpleCrawler.Core.Browser;
using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Core.Retry;

namespace SimpleCrawler.Core;

public class CrawlerOptions
{
    public int Concurrency { get; set; } = 8;
    public int ParseConcurrency { get; set; }
    public int MaxPages { get; set; } = 10000;
    public double CrawlDelay { get; set; } = 1;
    public bool RespectMetaRobots { get; set; } = true;
    public bool RespectRobotsTxt { get; set; } = true;
    public bool EnableSitemapDiscovery { get; set; } = true;

    // Cap on the decompressed response body size, in bytes. A value of 0 or less disables the cap.
    public long MaxResponseBodySize { get; set; } = 10 * 1024 * 1024;

    public IBrowserProfile BrowserProfile { get; set; } = new DefaultBrowserProfile();
    public ProxyPoolOptions? ProxyPool { get; set; }
    public RetryOptions Retry { get; set; } = new();

    public int EffectiveConcurrency => Math.Max(1, Concurrency);
    public int EffectiveParseConcurrency => ParseConcurrency > 0 ? ParseConcurrency : EffectiveConcurrency;
}
