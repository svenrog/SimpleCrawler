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

    [Option('t', "threads", Required = false, Default = 8, HelpText = "Parallel pages to fetch (default for both fetch and parse concurrency).")]
    public int Parallelism { get; set; } = 8;

    [Option('f', "fetchConcurrency", Required = false, Default = 0, HelpText = "Concurrent fetches in flight. 0 = use --threads. Raise above --threads to decouple fetching from parsing.")]
    public int FetchConcurrency { get; set; }

    [Option('p', "parseConcurrency", Required = false, Default = 0, HelpText = "Concurrent page parses. 0 = use --threads.")]
    public int ParseConcurrency { get; set; }

    [Option('m', "maxPages", Required = false, Default = 10000, HelpText = "Max pages to visit.")]
    public int MaxPages { get; set; } = 10000;

    [Option('d', "delay", Required = false, Default = 0, HelpText = "The crawl delay (in seconds)")]
    public double CrawlDelay { get; set; } = 0;

    [Option('r', "respectRobots", Required = false, Default = true, HelpText = "If crawling should respect meta robots and robots.txt rules.")]
    public bool RespectRobots { get; set; } = true;

    [Option('a', "userAgent", Required = false, HelpText = "This sets the user agent of the browser.")]
    public string? UserAgent { get; set; }
}
