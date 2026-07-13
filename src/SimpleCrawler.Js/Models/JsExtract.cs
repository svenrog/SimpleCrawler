using System.Text.Json;

namespace SimpleCrawler.Js.Models;

/// <summary>
/// The crawl-essential extract from a rendered page, plus <paramref name="Collectors"/> — the per-collector
/// JSON slices produced in-page by registered DOM collectors, keyed by collector, or <c>null</c> when none
/// are registered. Neutral: the renderer knows nothing of what any collector captures.
/// </summary>
public sealed record JsExtract(
    string? CanonicalHref,
    string? RobotsContent,
    IReadOnlyList<string?> LinkHrefs,
    IReadOnlyDictionary<string, JsonElement>? Collectors = null);
