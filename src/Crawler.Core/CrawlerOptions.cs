using Crawler.Core.Browser;

namespace Crawler.Core;

public class CrawlerOptions
{
    public int Concurrency { get; set; } = 8;
    public int ParseConcurrency { get; set; }
    public int MaxPages { get; set; } = 10000;
    public double CrawlDelay { get; set; } = 1;
    public bool RespectMetaRobots { get; set; } = true;
    public bool RespectRobotsTxt { get; set; } = true;
    public bool EnableSitemapDiscovery { get; set; } = true;
    public IBrowserProfile BrowserProfile { get; set; } = new DefaultBrowserProfile();
    public int EffectiveConcurrency => Math.Max(1, Concurrency);
    public int EffectiveParseConcurrency => ParseConcurrency > 0 ? ParseConcurrency : EffectiveConcurrency;
}
