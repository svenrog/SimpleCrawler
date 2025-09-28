using CommandLine;

namespace SimpleCrawler;

public sealed class Options
{
    [Option('e', "entryPoint", Required = true, Default = "http://127.0.0.1/", HelpText = "First page to visit")]
    public string Entry { get; set; } = "http://127.0.0.1/";

    [Option('c', "cookie", Required = false, HelpText = "Sets cookie header")]
    public string? Cookie { get; set; }

    [Option('o', "outputFile", Required = true, HelpText = "The file to output to.")]
    public string Output { get; set; } = string.Empty;

    [Option('t', "threads", Required = false, Default = 8, HelpText = "Parallel pages to fetch.")]
    public int Parallelism { get; set; } = 8;

    [Option('m', "maxPages", Required = false, Default = 10000, HelpText = "Max pages to visit.")]
    public int MaxPages { get; set; } = 10000;

    [Option('d', "delay", Required = false, Default = 0, HelpText = "The crawl delay (in seconds)")]
    public double CrawlDelay { get; set; } = 0;

    [Option('r', "respectMetaRobots", Required = false, Default = true, HelpText = "If crawling should respect meta robots.")]
    public bool RespectMetaRobots { get; set; } = true;
}
