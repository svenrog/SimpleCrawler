using SimpleCrawler.Core.Collectors;

namespace SimpleCrawler.Core.Models;

/// <summary>
/// The crawl-essential data a backend extracts from one page in its single parse pass: the canonical URL,
/// meta-robots directive, and outgoing link hrefs. <paramref name="Dom"/> is an opaque handle to that page's
/// DOM material for any registered <see cref="IDomCollector"/> to consume — <c>null</c> unless a DOM collector
/// is registered — so the pipeline can route DOM data to collectors without knowing what they collect.
/// </summary>
public readonly record struct PageExtract(
    string? CanonicalHref,
    RobotsRules Robots,
    IReadOnlyList<string?> LinkHrefs,
    IDomDispatch? Dom = null);
