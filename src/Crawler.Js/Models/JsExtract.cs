namespace Crawler.Js.Models;

public sealed record JsExtract(string? CanonicalHref, string? RobotsContent, IReadOnlyList<string?> LinkHrefs);
