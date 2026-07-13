using SimpleCrawler.Core.Collectors;
using SimpleCrawler.Core.Models;
using System.Text.Json;

namespace SimpleCrawler.Core.Helpers;

/// <summary>
/// Builds the one in-browser evaluation a headless backend runs per page: it returns every anchor href, the
/// canonical link, and the meta-robots directive, collapsing one protocol round-trip per element into one per
/// page. When DOM collectors are registered, <see cref="Compose"/> also injects their fragments (see
/// <see cref="DomScriptComposer"/>) so their data rides the same single pass.
///
/// The result is returned as a <c>JSON.stringify</c> string, not a live object: Playwright's evaluate
/// protocol injects reference-tracking <c>$id</c> keys into every object it serializes, which would otherwise
/// leak into a generically-enumerated collector result. Serializing in-page sidesteps that (and matches how
/// the in-process JS backends already return their extract).
/// </summary>
public static class RenderedPageExtractor
{
    /// <summary>
    /// The core extractor script, composed once from the registered <paramref name="collectors"/>. Their
    /// fragments populate <c>out.collectors</c> alongside the crawl-essential links/canonical/robots, each
    /// isolated so a faulty fragment can never break the core extraction.
    /// </summary>
    public static string Compose(IReadOnlyList<IRenderedDomCollector> collectors)
    {
        return $$"""
            () => {
                const anchors = document.querySelectorAll('a[href]');
                const links = new Array(anchors.length);
                for (let i = 0; i < anchors.length; i++) {
                    links[i] = anchors[i].getAttribute('href');
                }
                const canonical = document.querySelector("link[rel='canonical']");
                const robots = document.querySelector("meta[name='robots']");
                const out = {
                    links: links,
                    canonical: canonical ? canonical.getAttribute('href') : null,
                    robots: robots ? robots.getAttribute('content') : null
                };
                {{DomScriptComposer.CollectorBlock(collectors)}}
                return JSON.stringify(out);
            }
            """;
    }

    public static PageExtract Parse(JsonElement element)
    {
        var links = new List<string?>();
        if (element.TryGetProperty("links", out var linksElement) && linksElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in linksElement.EnumerateArray())
                links.Add(item.ValueKind == JsonValueKind.String ? item.GetString() : null);
        }

        var canonical = GetString(element, "canonical");
        var robots = IndexingHelper.ParseMetaRobots(GetString(element, "robots"));
        var collectors = DomScriptComposer.ReadCollectors(element);
        var dom = new RenderedDomDispatch(collectors);

        return new PageExtract(canonical, robots, links, dom);
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}
