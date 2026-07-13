using SimpleCrawler.Core.Models;
using System.Text.Json;

namespace SimpleCrawler.Core.Helpers;

/// <summary>
/// Reads the DOM half of a <see cref="PageSignals"/> (script sources, meta tags, JSON-LD blocks) out of the
/// JSON that <see cref="Collectors.PageSignalsCollector.DomScript"/> produces in-page. Kept next to that
/// collector's static-DOM walk so the two forms of the same extraction stay shaped identically.
/// </summary>
public static class PageSignalsParser
{
    /// <summary>Reads script sources, meta tags, and JSON-LD blocks from <paramref name="element"/>.</summary>
    public static PageSignals Read(JsonElement element)
    {
        var signals = new PageSignals();

        if (element.TryGetProperty("scriptSources", out var scriptSources) && scriptSources.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in scriptSources.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String)
                    signals.ScriptSources.Add(item.GetString()!);
        }

        if (element.TryGetProperty("jsonLdBlocks", out var jsonLd) && jsonLd.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in jsonLd.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String)
                    signals.JsonLdBlocks.Add(item.GetString()!);
        }

        if (element.TryGetProperty("metaTags", out var metaTags) && metaTags.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in metaTags.EnumerateObject())
                if (property.Value.ValueKind == JsonValueKind.String)
                    signals.MetaTags[property.Name] = property.Value.GetString()!;
        }

        return signals;
    }
}
