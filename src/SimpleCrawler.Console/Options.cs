using CommandLine;

namespace SimpleCrawler.Console;

public sealed class Options
{
    [Option('e', "entryPoint", Required = true, Min = 1, HelpText = "Entry page(s) to visit. Repeat -e or pass several after one -e (e.g. -e https://a.com -e https://b.com). The crawl stays within the exact hosts listed.")]
    public IEnumerable<string> Entry { get; set; } = [];

    [Option('c', "cookie", Required = false, HelpText = "Sets cookie header")]
    public string? Cookie { get; set; }

    [Option('H', "header", Required = false, HelpText = "Extra request header as 'Name: Value'. Repeat -H for several. Sent on every request across all backends; overrides a matching default header.")]
    public IEnumerable<string> Headers { get; set; } = [];

    [Option('o', "outputFile", Required = true, HelpText = "The file to output to.")]
    public string Output { get; set; } = string.Empty;

    [Option("report", Required = false, HelpText = "Optional path to write a per-URL JSON report (status code, fetch/parse timing, size, link count, outcome) covering every fetched page, including failures.")]
    public string? Report { get; set; }

    [Option("captureSignals", Required = false, Default = false, HelpText = "Capture per-page HTTP/DOM signals (response headers, cookie names, script sources, meta tags, JSON-LD blocks) into the --report output. Off by default: increases checkpoint size on large crawls.")]
    public bool CaptureSignals { get; set; }

    [Option('m', "maxPages", Required = false, Default = 10000, HelpText = "Max pages to visit.")]
    public int MaxPages { get; set; } = 10000;

    [Option("maxDepth", Required = false, Default = 0, HelpText = "Max link depth from the entry points (entries are depth 0, each followed link one deeper). 0 = no limit.")]
    public int MaxDepth { get; set; }

    [Option("normalizeUrls", Required = false, Default = true, HelpText = "Canonicalize URLs before de-duplication: drop #fragment, lowercase scheme/host, remove default port, collapse a trailing slash. Query string is left as-is. Pass 'false' to disable.")]
    public bool NormalizeUrls { get; set; } = true;

    [Option("include", Required = false, HelpText = "Only crawl discovered links whose path matches this robots.txt-style pattern (* wildcard, $ end-anchor). Repeat --include for several; entry points are always crawled.")]
    public IEnumerable<string> Include { get; set; } = [];

    [Option("exclude", Required = false, HelpText = "Skip discovered links whose path matches this robots.txt-style pattern (* wildcard, $ end-anchor). Repeat --exclude for several; an exclude out-matches an include of equal length.")]
    public IEnumerable<string> Exclude { get; set; } = [];

    [Option('d', "delay", Required = false, Default = 1, HelpText = "Minimum seconds between requests (floor; robots.txt can raise it). 0 removes throttling.")]
    public double CrawlDelay { get; set; } = 1;

    [Option('r', "respectRobots", Required = false, Default = true, HelpText = "If crawling should respect meta robots and robots.txt rules.")]
    public bool RespectRobots { get; set; } = true;

    [Option('a', "userAgent", Required = false, HelpText = "This sets the user agent of the browser.")]
    public string? UserAgent { get; set; }

    [Option('p', "proxy", Required = false, HelpText = "A proxy to use for requests (or a reference to a list of proxies)")]
    public string? Proxy { get; set; }

    [Option("retries", Required = false, Default = 3, HelpText = "Max retry attempts per request before surfacing the failure (applies with or without proxies).")]
    public int Retries { get; set; } = 3;

    [Option("retryDelay", Required = false, Default = 500, HelpText = "Base backoff in milliseconds between retries; grows exponentially with jitter up to --maxRetryDelay.")]
    public int RetryDelay { get; set; } = 500;

    [Option("maxRetryDelay", Required = false, Default = 30000, HelpText = "Upper bound in milliseconds on the backoff between retries.")]
    public int MaxRetryDelay { get; set; } = 30000;

    [Option("attemptTimeout", Required = false, Default = 100000, HelpText = "Per-attempt timeout in milliseconds; a slower attempt is cancelled and retried. 0 disables it.")]
    public int AttemptTimeout { get; set; } = 100000;

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

    [Option("adaptiveThrottle", Required = false, Default = true, HelpText = "Automatically slow a host after repeated 429/503 responses (honouring Retry-After) and ease back off on sustained success. Pass 'false' to disable.")]
    public bool AdaptiveThrottle { get; set; } = true;

    [Option("checkpoint", Required = false, HelpText = "Path to a checkpoint file. Progress is saved here periodically and on Ctrl+C; if the file already exists for the same entry points, the crawl resumes from it.")]
    public string? Checkpoint { get; set; }

    [Option("progress", Required = false, Default = true, HelpText = "Periodically log a crawl-time estimate (pages done, projected total, ETA) inferred from how fast new links are still being found. Pass 'false' to disable.")]
    public bool Progress { get; set; } = true;

    [Option("progressInterval", Required = false, Default = 15, HelpText = "Seconds between progress/ETA log lines.")]
    public int ProgressInterval { get; set; } = 15;

    [Option("progressConfirm", Required = false, Default = 60, HelpText = "Seconds the pending queue must keep shrinking before an ETA is shown. Higher values wait for more certainty (a late burst of new links won't have produced a confident-but-wrong estimate); lower values show an ETA sooner.")]
    public int ProgressConfirm { get; set; } = 60;

#if JS
    [Option("fetch", Required = false, HelpText = "Apply if rendering should enable Fetch API")]
    public bool Fetch { get; set; }

    [Option("streaming", Required = false, HelpText = "Apply if rendering should emulate WHATWG Streams (required for Next.js sites)")]
    public bool Streaming { get; set; }

    [Option("indexedDb", Required = false, HelpText = "Apply if rendering should stub IndexedDb")]
    public bool IndexedDb { get; set; }

    [Option("webgl", Required = false, HelpText = "Apply if rendering should stub WebGL (lets map/3D sites render instead of tripping their error boundary)")]
    public bool WebGl { get; set; }
#endif
}
