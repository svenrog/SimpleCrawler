namespace SimpleCrawler.Js.Abstractions;

/// <summary>
/// Where a module specifier points, decided the way a browser decides it: the page's
/// <see cref="ImportMap"/> first, then URL rules, and — for a bare specifier the map does not cover — nowhere.
/// Both engine loaders resolve through this so a page's imports mean the same thing on either.
/// </summary>
public static class ModuleSpecifier
{
    public static Uri Resolve(string specifier, Uri referrer, ImportMap? map)
    {
        var mapped = map?.Resolve(specifier);
        if (mapped is not null)
            return mapped;

        // A bare specifier no import map covers. A browser refuses to resolve one at all; the render answers
        // an address nothing can be fetched from, so the loader substitutes an empty module — the same
        // partial result, without asking the target for a path it never published.
        return TryResolveAddress(specifier, referrer, out var resolved)
            ? resolved
            : new Uri("about:" + Uri.EscapeDataString(specifier));
    }

    /// <summary>
    /// A URL or a path against <paramref name="baseUri"/>, which is what both a specifier and an import map's
    /// address may be. <c>false</c> for a bare one, which is neither.
    /// </summary>
    public static bool TryResolveAddress(string address, Uri baseUri, out Uri resolved)
    {
        // Path-relative forms are decided before an absolute parse, because on Unix "/assets/x.js" parses as
        // an absolute file:// URI and is in fact a path against the base.
        if (address.StartsWith('/')
            || address.StartsWith("./", StringComparison.Ordinal)
            || address.StartsWith("../", StringComparison.Ordinal))
        {
            return Uri.TryCreate(baseUri, address, out resolved!);
        }

        if (Uri.TryCreate(address, UriKind.Absolute, out var absolute) && !absolute.IsFile)
        {
            resolved = absolute;
            return true;
        }

        resolved = null!;
        return false;
    }

    /// <summary>
    /// The location to resolve a module's own imports against: its own, or the page when it has none to give.
    /// A module built from an object URL is the case that matters — the token carries no path, so its imports
    /// belong to the page, which is the origin a browser's <c>blob:</c> URL carries.
    /// </summary>
    public static Uri ReferrerOrBase(Uri? referrer, Uri baseUri)
        => referrer is not null && (referrer.Scheme == Uri.UriSchemeHttp || referrer.Scheme == Uri.UriSchemeHttps)
            ? referrer
            : baseUri;
}
