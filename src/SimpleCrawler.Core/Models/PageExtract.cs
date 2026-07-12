namespace SimpleCrawler.Core.Models;

/// <summary>
/// <paramref name="Signals"/> is <c>null</c> unless the crawl runs with
/// <see cref="CrawlerOptions.CapturePageSignals"/> on; when present it carries the DOM-derived half
/// (script sources, meta tags, JSON-LD) of the page's <see cref="PageSignals"/> — the HTTP-derived half
/// (headers, cookies) is captured earlier, at fetch time, via <c>AbstractCrawler.ReportSignals</c>.
/// </summary>
public readonly record struct PageExtract(
    string? CanonicalHref,
    RobotsRules Robots,
    IReadOnlyList<string?> LinkHrefs,
    PageSignals? Signals = null);
