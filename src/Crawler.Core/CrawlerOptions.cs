namespace Crawler.Core;

public class CrawlerOptions
{
    public string? UserAgent { get; set; }
    public int Concurrency { get; set; } = 8;
    public int ParseConcurrency { get; set; }
    public int MaxPages { get; set; } = 10000;
    public double CrawlDelay { get; set; } = 0;
    public bool RespectMetaRobots { get; set; } = true;
    public bool RespectRobotsTxt { get; set; } = true;
    public bool BlockNonEssentialResources { get; set; } = true;

    public int EffectiveConcurrency => Math.Max(1, Concurrency);
    public int EffectiveParseConcurrency => ParseConcurrency > 0 ? ParseConcurrency : EffectiveConcurrency;
}
