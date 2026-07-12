using SimpleCrawler.Core.Models;
using System.Text.Json;

namespace SimpleCrawler.Core.Helpers;

/// <summary>
/// Reads the DOM half of a <see cref="PageSignals"/> (script sources, meta tags, JSON-LD blocks) out of
/// the JSON an in-browser extractor produces. Shared by every backend that captures signals from a
/// rendered tree — the headless <see cref="RenderedPageExtractor"/> and the in-process JS renderer —
/// which differ only in where the signal object sits in their result, not in how its fields are shaped.
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
