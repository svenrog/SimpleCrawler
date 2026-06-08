namespace Crawler.Core;

public class CrawlerOptions
{
    public string? UserAgent { get; set; }
    public int Parallelism { get; set; } = 8;
    public int FetchConcurrency { get; set; }
    public int ParseConcurrency { get; set; }
    public int MaxPages { get; set; } = 10000;
    public double CrawlDelay { get; set; } = 0;
    public bool RespectMetaRobots { get; set; } = true;
    public bool RespectRobotsTxt { get; set; } = true;
    public bool BlockNonEssentialResources { get; set; } = true;

    public int EffectiveFetchConcurrency => FetchConcurrency > 0 ? FetchConcurrency : Math.Max(1, Parallelism);
    public int EffectiveParseConcurrency => ParseConcurrency > 0 ? ParseConcurrency : Math.Max(1, Parallelism);
}
