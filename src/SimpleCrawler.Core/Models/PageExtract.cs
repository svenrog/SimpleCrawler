namespace SimpleCrawler.Core.Models;

/// <summary>
/// <paramref name="Signals"/> is <c>null</c> unless a signal-capturing <c>ICrawlCollector</c> is
/// registered; when present it carries the DOM-derived half (script sources, meta tags, JSON-LD) that a
/// backend extracts in its single parse pass, for a collector to consume. The HTTP-derived half (headers,
/// cookies) reaches the collector separately at fetch time via <c>ResponseSignal</c>. Backends populate
/// this only when <c>CaptureSignals</c> (i.e. any collector is registered), so an uncollected crawl pays
/// nothing.
/// </summary>
public readonly record struct PageExtract(
    string? CanonicalHref,
    RobotsRules Robots,
    IReadOnlyList<string?> LinkHrefs,
    PageSignals? Signals = null);
