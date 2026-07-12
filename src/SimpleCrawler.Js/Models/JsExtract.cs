using SimpleCrawler.Core.Models;

namespace SimpleCrawler.Js.Models;

public sealed record JsExtract(string? CanonicalHref, string? RobotsContent, IReadOnlyList<string?> LinkHrefs, PageSignals? Signals = null);
