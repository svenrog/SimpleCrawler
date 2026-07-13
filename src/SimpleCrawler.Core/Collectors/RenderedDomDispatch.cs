using SimpleCrawler.Core.Models;
using System.Text.Json;

namespace SimpleCrawler.Core.Collectors;

/// <summary>
/// <see cref="IDomDispatch"/> for the rendered backends: each collector's <see cref="IRenderedDomCollector.DomScript"/>
/// ran in-page and its JSON result was keyed by <see cref="IDomCollector.Key"/>. Dispatch hands each collector
/// its own slice; a collector whose fragment produced nothing — it threw, or returned an unserializable value —
/// has no slice and is skipped.
/// </summary>
public sealed class RenderedDomDispatch : IDomDispatch
{
    private readonly IReadOnlyDictionary<string, JsonElement> _results;

    public RenderedDomDispatch(IReadOnlyDictionary<string, JsonElement> results)
    {
        _results = results;
    }

    public ValueTask Dispatch(UrlReport report, IDomCollector collector, string resolvedUrl)
    {
        if (collector is not IRenderedDomCollector renderedCollector)
            return ValueTask.CompletedTask;

        return _results.TryGetValue(collector.Key, out var result) && result.ValueKind != JsonValueKind.Null
            ? renderedCollector.OnRendered(report, result, resolvedUrl)
            : ValueTask.CompletedTask;
    }
}
