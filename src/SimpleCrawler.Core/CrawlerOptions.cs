using SimpleCrawler.Core.Browser;
using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Progress;
using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Core.Retry;
using SimpleCrawler.Core.Throttling;

namespace SimpleCrawler.Core;

public class CrawlerOptions
{
    public int Concurrency { get; set; } = 8;
    public int ParseConcurrency { get; set; }
    public int MaxPages { get; set; } = 10000;

    /// <summary>
    /// Maximum link depth from the entry points, where the entries are depth 0 and each followed link is one
    /// deeper. A value of 0 imposes no limit.
    /// </summary>
    public int MaxDepth { get; set; }

    public double CrawlDelay { get; set; } = 1;
    public bool RespectMetaRobots { get; set; } = true;
    public bool RespectRobotsTxt { get; set; } = true;
    public bool EnableSitemapDiscovery { get; set; } = true;

    /// <summary>
    /// Canonicalizes discovered URLs before they are deduplicated and queued: drops the fragment, lowercases
    /// scheme and host, removes the default port, and collapses a trailing slash. The query string is left
    /// untouched.
    /// </summary>
    public bool NormalizeUrls { get; set; } = true;

    /// <summary>
    /// robots.txt-style path patterns (<c>*</c> wildcard, <c>$</c> end-anchor) that a discovered link must
    /// match to be crawled. When empty, every link is allowed unless excluded.
    /// </summary>
    public IReadOnlyList<string> IncludePatterns { get; set; } = [];

    /// <summary>
    /// robots.txt-style path patterns (<c>*</c> wildcard, <c>$</c> end-anchor) that exclude a discovered link
    /// from the crawl. An exclude out-matches an include by the same longest-match rule robots.txt uses.
    /// </summary>
    public IReadOnlyList<string> ExcludePatterns { get; set; } = [];

    /// <summary>
    /// Cap on the decompressed response body size, in bytes. A value of 0 or less disables the cap.
    /// </summary>
    public long MaxResponseBodySize { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Opt-in: capture per-page HTTP/DOM signals (response headers, cookie names, script sources, meta
    /// tags, JSON-LD blocks) into <see cref="Models.UrlReport.Signals"/>. Off by default because
    /// <c>UrlReport</c> is checkpointed and the whole checkpoint is rewritten on every autosave, so
    /// capturing for every page would bloat every autosave with the full crawl history rather than
    /// just the in-flight URLs.
    /// </summary>
    public bool CapturePageSignals { get; set; }

    public IBrowserProfile BrowserProfile { get; set; } = new DefaultBrowserProfile();
    public ProxyPoolOptions? ProxyPool { get; set; }
    public RetryOptions Retry { get; set; } = new();
    public ThrottleOptions Throttling { get; set; } = new();
    public CheckpointOptions Checkpoint { get; set; } = new();
    public ProgressOptions Progress { get; set; } = new();

    public int EffectiveConcurrency => Math.Max(1, Concurrency);
    public int EffectiveParseConcurrency => ParseConcurrency > 0 ? ParseConcurrency : EffectiveConcurrency;
}
