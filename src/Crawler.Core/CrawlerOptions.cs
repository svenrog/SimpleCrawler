namespace Crawler.Core;

public class CrawlerOptions
{
    public string Entry { get; set; } = "http://127.0.0.1/";
    public int Parallelism { get; set; } = 8;
    public int MaxPages { get; set; } = 10000;
    public double CrawlDelay { get; set; } = 0;

    public bool RespectMetaRobots { get; set; } = true;
}
