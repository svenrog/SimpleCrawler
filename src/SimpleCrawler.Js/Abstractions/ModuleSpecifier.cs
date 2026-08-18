namespace SimpleCrawler.Js.Abstractions;

/// <summary>
/// Where a module specifier points, decided the way a browser decides it: the page's
/// <see cref="ImportMap"/> first, then URL rules, and — for a bare specifier the map does not cover — nowhere.
/// Both engine loaders resolve through this so a page's imports mean the same thing on either.
/// </summary>
public static class ModuleSpecifier
{
    /// <summary>
    /// A bare specifier no import map covers. A browser refuses to resolve one at all; the render answers an
    /// address nothing can be fetched from, so the loader substitutes an empty module — the same partial
    /// result, without asking the target for a path it never published.
    /// </summary>
    private const string _unmappedScheme = "about:";

    public static Uri Resolve(string specifier, Uri referrer, ImportMap? map)
    {
        var mapped = map?.Resolve(specifier);
        if (mapped is not null)
            return mapped;

        // Path-relative forms are decided before an absolute parse, because on Unix "/assets/x.js" parses as
        // an absolute file:// URI and is in fact a path against the referrer.
        if (IsPathRelative(specifier))
            return new Uri(referrer, specifier);

        if (Uri.TryCreate(specifier, UriKind.Absolute, out var absolute) && !absolute.IsFile)
            return absolute;

        return new Uri(_unmappedScheme + Uri.EscapeDataString(specifier));
    }

    private static bool IsPathRelative(string specifier)
        => specifier.StartsWith('/') || specifier.StartsWith("./", StringComparison.Ordinal) || specifier.StartsWith("../", StringComparison.Ordinal);
}
