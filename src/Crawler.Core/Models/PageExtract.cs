namespace Crawler.Core.Models;

public readonly record struct PageExtract(string? CanonicalUrl, RobotsRules Robots, IReadOnlyList<string?> LinkHrefs);
