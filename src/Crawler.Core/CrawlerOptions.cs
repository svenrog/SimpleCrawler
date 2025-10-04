namespace Crawler.Core;

public class CrawlerOptions
{
    public string? UserAgent { get; set; }
    public int Parallelism { get; set; } = 8;
    public int MaxPages { get; set; } = 10000;
    public double CrawlDelay { get; set; } = 0;
    public bool RespectMetaRobots { get; set; } = true;
    public bool RespectRobotsTxt { get; set; } = true;
}
