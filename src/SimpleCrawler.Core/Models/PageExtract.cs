namespace SimpleCrawler.Core.Models;

public readonly record struct PageExtract(string? CanonicalHref, RobotsRules Robots, IReadOnlyList<string?> LinkHrefs);
