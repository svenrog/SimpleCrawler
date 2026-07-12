using SimpleCrawler.Core.Models;
using System.Text.Json;

namespace SimpleCrawler.Core.Helpers;

/// <summary>
/// One in-browser evaluation returns every anchor href, the canonical link and the meta-robots
/// directive, collapsing one protocol round-trip per element into one round-trip per page. When
/// invoked with <c>captureSignals</c> true, the same walk also collects script sources, meta tags,
/// and JSON-LD blocks, so opting out costs nothing extra per page.
///
/// The result is returned as a <c>JSON.stringify</c> string, not a live object: Playwright's evaluate
/// protocol injects reference-tracking <c>$id</c> keys into every object it serializes, which would
/// otherwise leak into the generically-enumerated meta-tag map. Serializing in-page sidesteps that (and
/// matches how the in-process JS backends already return their extract).
/// </summary>
public static class RenderedPageExtractor
{
    public const string Script = """
        (captureSignals) => {
            const anchors = document.querySelectorAll('a[href]');
            const links = new Array(anchors.length);
            for (let i = 0; i < anchors.length; i++) {
                links[i] = anchors[i].getAttribute('href');
            }
            const canonical = document.querySelector("link[rel='canonical']");
            const robots = document.querySelector("meta[name='robots']");

            let signals = null;
            if (captureSignals) {
                const scriptSources = [];
                const jsonLdBlocks = [];
                for (const script of document.querySelectorAll('script')) {
                    const src = script.getAttribute('src');
                    if (src) {
                        scriptSources.push(src);
                    } else if ((script.getAttribute('type') || '').toLowerCase() === 'application/ld+json') {
                        const text = (script.textContent || '').trim();
                        if (text) jsonLdBlocks.push(text);
                    }
                }
                const metaTags = {};
                for (const meta of document.querySelectorAll('meta')) {
                    const name = meta.getAttribute('name') || meta.getAttribute('property');
                    const content = meta.getAttribute('content');
                    if (name && content !== null) metaTags[name] = content;
                }
                signals = { scriptSources, metaTags, jsonLdBlocks };
            }

            return JSON.stringify({
                links: links,
                canonical: canonical ? canonical.getAttribute('href') : null,
                robots: robots ? robots.getAttribute('content') : null,
                signals: signals
            });
        }
        """;

    public static (string? CanonicalHref, string? RobotsContent, IReadOnlyList<string?> LinkHrefs, PageSignals? Signals) Parse(JsonElement element)
    {
        var links = new List<string?>();
        if (element.TryGetProperty("links", out var linksElement) && linksElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in linksElement.EnumerateArray())
                links.Add(item.ValueKind == JsonValueKind.String ? item.GetString() : null);
        }

        return (GetString(element, "canonical"), GetString(element, "robots"), links, ParseSignals(element));
    }

    private static PageSignals? ParseSignals(JsonElement element)
    {
        return element.TryGetProperty("signals", out var signalsElement) && signalsElement.ValueKind == JsonValueKind.Object
            ? PageSignalsParser.Read(signalsElement)
            : null;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}
