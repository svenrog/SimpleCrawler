using CommandLine;

namespace SimpleCrawler.Console;

public sealed class Options
{
    [Option('e', "entryPoint", Required = true, Min = 1, HelpText = "Entry page(s) to visit. Repeat -e or pass several after one -e (e.g. -e https://a.com -e https://b.com). The crawl stays within the exact hosts listed.")]
    public IEnumerable<string> Entry { get; set; } = [];

    [Option('c', "cookie", Required = false, HelpText = "Sets cookie header")]
    public string? Cookie { get; set; }

    [Option('o', "outputFile", Required = true, HelpText = "The file to output to.")]
    public string Output { get; set; } = string.Empty;

    [Option('m', "maxPages", Required = false, Default = 10000, HelpText = "Max pages to visit.")]
    public int MaxPages { get; set; } = 10000;

    [Option('d', "delay", Required = false, Default = 1, HelpText = "Minimum seconds between requests (floor; robots.txt can raise it). 0 removes throttling.")]
    public double CrawlDelay { get; set; } = 1;

    [Option('r', "respectRobots", Required = false, Default = true, HelpText = "If crawling should respect meta robots and robots.txt rules.")]
    public bool RespectRobots { get; set; } = true;

    [Option('a', "userAgent", Required = false, HelpText = "This sets the user agent of the browser.")]
    public string? UserAgent { get; set; }

    [Option('p', "proxy", Required = false, HelpText = "A proxy to use for requests (or a reference to a list of proxies)")]
    public string? Proxy { get; set; }

    [Option("proxyRetries", Required = false, Default = 3, HelpText = "Max proxy retries per request before surfacing the failure.")]
    public int ProxyRetries { get; set; } = 3;

    [Option("proxyCooldown", Required = false, Default = 60, HelpText = "Seconds a failing proxy is benched before being retried.")]
    public int ProxyCooldown { get; set; } = 60;

    [Option("proxyMinHealthy", Required = false, Default = 0.25, HelpText = "Fraction of proxies that must stay healthy; below this the crawl aborts.")]
    public double ProxyMinHealthy { get; set; } = 0.25;

    [Option('i', "impersonate", Required = false, Default = BrowserImpersonation.None, HelpText = "Impersonate a real browser to reduce bot-detection blocks (e.g. 403). Values: none, chrome.")]
    public BrowserImpersonation Impersonate { get; set; }

    [Option('t', "concurrency", Required = false, Default = 8, HelpText = "Concurrent fetches in flight.")]
    public int Concurrency { get; set; } = 8;

    [Option("parseConcurrency", Required = false, Default = 0, HelpText = "Concurrent page parses. 0 = match --concurrency; lowering it below --concurrency can improve throughput on parse-heavy sites.")]
    public int ParseConcurrency { get; set; }
}
