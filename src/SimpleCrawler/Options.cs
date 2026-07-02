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

    [Option('t', "concurrency", Required = false, Default = 8, HelpText = "Concurrent fetches in flight.")]
    public int Concurrency { get; set; } = 8;

    [Option('p', "parseConcurrency", Required = false, Default = 0, HelpText = "Concurrent page parses. 0 = match --concurrency; lowering it below --concurrency can improve throughput on parse-heavy sites.")]
    public int ParseConcurrency { get; set; }

    [Option('m', "maxPages", Required = false, Default = 10000, HelpText = "Max pages to visit.")]
    public int MaxPages { get; set; } = 10000;

    [Option('d', "delay", Required = false, Default = 1, HelpText = "Minimum seconds between requests (floor; robots.txt can raise it). 0 removes throttling.")]
    public double CrawlDelay { get; set; } = 1;

    [Option('r', "respectRobots", Required = false, Default = true, HelpText = "If crawling should respect meta robots and robots.txt rules.")]
    public bool RespectRobots { get; set; } = true;

    [Option('a', "userAgent", Required = false, HelpText = "This sets the user agent of the browser.")]
    public string? UserAgent { get; set; }

    [Option('i', "impersonate", Required = false, Default = BrowserImpersonation.None, HelpText = "Impersonate a real browser to reduce bot-detection blocks (e.g. 403). Values: none, chrome.")]
    public BrowserImpersonation Impersonate { get; set; }
}
