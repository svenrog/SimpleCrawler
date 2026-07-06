using System.Text.Json;

namespace Crawler.Core.Helpers;

// One in-browser evaluation returns every anchor href, the canonical link and the meta-robots
// directive, collapsing one protocol round-trip per element into one round-trip per page.
public static class RenderedPageExtractor
{
    public const string Script = """
        () => {
            const anchors = document.querySelectorAll('a[href]');
            const links = new Array(anchors.length);
            for (let i = 0; i < anchors.length; i++) {
                links[i] = anchors[i].getAttribute('href');
            }
            const canonical = document.querySelector("link[rel='canonical']");
            const robots = document.querySelector("meta[name='robots']");
            return {
                links: links,
                canonical: canonical ? canonical.getAttribute('href') : null,
                robots: robots ? robots.getAttribute('content') : null
            };
        }
        """;

    public static (string? CanonicalHref, string? RobotsContent, IReadOnlyList<string?> LinkHrefs) Parse(JsonElement element)
    {
        var links = new List<string?>();
        if (element.TryGetProperty("links", out var linksElement) && linksElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in linksElement.EnumerateArray())
                links.Add(item.ValueKind == JsonValueKind.String ? item.GetString() : null);
        }

        return (GetString(element, "canonical"), GetString(element, "robots"), links);
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}
