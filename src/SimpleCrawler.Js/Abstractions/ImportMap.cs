using SimpleCrawler.Core.Helpers;
using System.Text.Json;

namespace SimpleCrawler.Js.Abstractions;

/// <summary>
/// The page's own answer to "where does this module specifier live" — a <c>&lt;script type="importmap"&gt;</c>
/// read off the shell. Without one a bare specifier resolves nowhere: a browser refuses it rather than
/// treating it as a path, so fetching <c>&lt;page&gt;/@scope/pkg</c> asks the target for something no browser
/// ever asks it for and answers the render a 404 page.
/// <para>
/// Only the <c>imports</c> map is read. <c>scopes</c> and <c>integrity</c> address a per-referrer override and
/// a hash check, neither of which changes what a single-pass render can collect.
/// </para>
/// </summary>
public sealed class ImportMap
{
    private readonly Dictionary<string, Uri> _exact;
    private readonly List<KeyValuePair<string, Uri>> _prefixes;

    private ImportMap(Dictionary<string, Uri> exact, List<KeyValuePair<string, Uri>> prefixes)
    {
        _exact = exact;
        _prefixes = prefixes;
    }

    /// <summary>
    /// Reads a map from the script's own text, resolving every address against the document base URL as the
    /// spec does. Returns <c>null</c> for text that is not an object with a usable <c>imports</c> member —
    /// a malformed map is one a browser ignores, and a render that threw over it would lose the page.
    /// </summary>
    public static ImportMap? Parse(string? json, Uri baseUri)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("imports", out var imports)
                || imports.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var exact = new Dictionary<string, Uri>(StringComparer.Ordinal);
            var prefixes = new List<KeyValuePair<string, Uri>>();
            foreach (var entry in imports.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.String)
                    continue;

                var specifier = entry.Name;
                var address = entry.Value.GetString();
                if (string.IsNullOrEmpty(specifier) || string.IsNullOrEmpty(address))
                    continue;

                if (!TryResolveAddress(address, baseUri, out var target))
                    continue;

                // A key ending in '/' maps a whole subtree, and the spec requires its address to end in one
                // too — the remainder is appended to it verbatim.
                if (specifier[^1] == '/')
                {
                    if (target.AbsoluteUri[^1] == '/')
                        prefixes.Add(new KeyValuePair<string, Uri>(specifier, target));
                }
                else
                {
                    exact[specifier] = target;
                }
            }

            if (exact.Count == 0 && prefixes.Count == 0)
                return null;

            // Longest key first, so the most specific subtree wins where several match.
            prefixes.Sort(static (a, b) => b.Key.Length.CompareTo(a.Key.Length));
            return new ImportMap(exact, prefixes);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The address the map gives this specifier, or <c>null</c> for one it does not cover.
    /// </summary>
    public Uri? Resolve(string specifier)
    {
        if (_exact.TryGetValue(specifier, out var exact))
            return exact;

        foreach (var prefix in _prefixes)
        {
            if (!specifier.StartsWith(prefix.Key, StringComparison.Ordinal))
                continue;

            if (Uri.TryCreate(prefix.Value, specifier[prefix.Key.Length..], out var mapped))
                return mapped;
        }

        return null;
    }

    private static bool TryResolveAddress(string address, Uri baseUri, out Uri target)
    {
        if (UriHelper.TryCreateHttpAbsolute(address, out var absolute))
        {
            target = absolute;
            return true;
        }

        return Uri.TryCreate(baseUri, address, out target!);
    }
}
