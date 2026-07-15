using SimpleCrawler.Core.Helpers;
using System.Text;
using System.Text.Json;

namespace SimpleCrawler.Core.Collectors;

/// <summary>
/// Bridges registered <see cref="IDomCollector"/> fragments to the rendered backends' single in-page
/// evaluation. <see cref="CollectorBlock(IReadOnlyList{IRenderedDomCollector})"/> emits the JavaScript that
/// runs each collector's fragment and keys its result under
/// <see cref="IDomCollector.Key"/>; <see cref="ReadCollectors"/> reads those keyed
/// results back out of the returned envelope. Each fragment is invoked in its own scope, its result serialized
/// independently, and the whole wrapped so a fragment that throws or returns an unserializable value yields
/// <c>null</c> for its slice without disturbing the crawl-essential fields or the other collectors.
/// </summary>
public static class DomScriptComposer
{
    private static readonly IReadOnlyDictionary<string, JsonElement> _empty = new Dictionary<string, JsonElement>();

    /// <summary>
    /// JavaScript statements that populate <c>out.collectors</c> — an object keyed by collector — assuming an
    /// <c>out</c> variable already exists in the surrounding scope. Each slice is the fragment's result run
    /// through <c>JSON.stringify</c>, so <see cref="ReadCollectors"/> reparses it; wrapping each in its own
    /// <c>try</c> keeps one misbehaving fragment from poisoning the envelope.
    /// </summary>
    public static string CollectorBlock(IReadOnlyList<IRenderedDomCollector> collectors)
    {
        var fragments = new (string Key, string DomScript)[collectors.Count];
        for (var i = 0; i < collectors.Count; i++)
        {
            fragments[i] = (collectors[i].Key, collectors[i].DomScript);
        }

        return CollectorBlock(fragments);
    }

    /// <summary>
    /// <see cref="CollectorBlock(IReadOnlyList{IRenderedDomCollector})"/> over bare key/script pairs, for a
    /// caller composing a block without a crawl behind it. Composition only ever needed the key and the
    /// fragment; requiring a whole <see cref="IRenderedDomCollector"/> additionally required an
    /// <see cref="ICrawlCollector.OnResponse"/> and an <see cref="IRenderedDomCollector.OnRendered"/> that a
    /// caller driving <c>JsRenderer.CollectAsync</c> has no crawl to implement against, and a
    /// <see cref="Models.UrlReport"/> to name in order to no-op them.
    /// </summary>
    public static string CollectorBlock(IReadOnlyList<(string Key, string DomScript)> fragments)
    {
        var builder = new StringBuilder("out.collectors={};");
        foreach (var (fragmentKey, domScript) in fragments)
        {
            var key = JsonLiteral.String(fragmentKey);
            builder.Append("try{out.collectors[").Append(key).Append("]=JSON.stringify((")
                   .Append(domScript).Append(")());}catch(e){out.collectors[").Append(key).Append("]=null;}");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Reads the <c>collectors</c> object from a rendered extraction envelope into per-collector slices. Each
    /// value is itself a JSON string (the fragment's independently-serialized result) or <c>null</c>; this
    /// reparses each into a standalone <see cref="JsonElement"/>. Returns an empty map when absent.
    /// </summary>
    public static IReadOnlyDictionary<string, JsonElement> ReadCollectors(JsonElement envelope)
    {
        if (!envelope.TryGetProperty("collectors", out var collectors) || collectors.ValueKind != JsonValueKind.Object)
            return _empty;

        Dictionary<string, JsonElement>? results = null;
        foreach (var property in collectors.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
                continue;

            var slice = property.Value.GetString();
            if (string.IsNullOrEmpty(slice))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(slice);
                (results ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal))[property.Name] = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                // A fragment returned malformed JSON; skip its slice rather than fail the page.
            }
        }

        return results ?? _empty;
    }
}
