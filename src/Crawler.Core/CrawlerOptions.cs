namespace Crawler.Core;

public class CrawlerOptions
{
    public string Entry { get; set; } = "http://127.0.0.1/";
    public int Parallellism { get; set; } = 8;
    public int MaxPages { get; set; } = 10000;
}
