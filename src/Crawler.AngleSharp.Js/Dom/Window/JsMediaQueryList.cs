namespace Crawler.AngleSharp.Js.Dom.Window;

// A MediaQueryList that presents the crawl as a desktop viewport. Width/height queries are evaluated
// against that viewport so responsive SPAs pick their desktop layout — which matters because mobile
// layouts hide their navigation behind a burger drawer (a lazy, interaction-gated chunk), so the
// category links never reach the DOM. Non-dimensional features (hover, prefers-*, orientation) stay
// unmatched: we can't model them and a false keeps their conditional UI off the crawl.
public sealed class JsMediaQueryList
{
    private const int _viewportWidth = 1920;
    private const int _viewportHeight = 1080;

    public JsMediaQueryList(string query)
    {
        media = query;
        matches = Evaluate(query);
    }

    public string media { get; }
    public bool matches { get; }
    public object? onchange { get; set; }

    public void addEventListener(object? type = null, object? listener = null, object? options = null) { }
    public void removeEventListener(object? type = null, object? listener = null, object? options = null) { }
    public void addListener(object? listener = null) { }
    public void removeListener(object? listener = null) { }
    public bool dispatchEvent(object? @event = null) => false;

    // A media query is a conjunction of features joined by "and"; we only model the dimensional ones. A
    // query with at least one width/height feature matches when the viewport satisfies all of them; a
    // query with none (hover, prefers-color-scheme, …) stays unmatched.
    private static bool Evaluate(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;

        var sawDimension = false;
        foreach (var clause in query.Split("and", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryParseDimension(clause, out var isMin, out var isWidth, out var pixels))
                continue;

            sawDimension = true;
            var actual = isWidth ? _viewportWidth : _viewportHeight;
            if (isMin ? actual < pixels : actual > pixels)
                return false;
        }

        return sawDimension;
    }

    private static bool TryParseDimension(string clause, out bool isMin, out bool isWidth, out int pixels)
    {
        isMin = isWidth = false;
        pixels = 0;

        var colon = clause.IndexOf(':');
        if (colon < 0)
            return false;

        var feature = clause.AsSpan(0, colon).Trim(" (".AsSpan());
        if (feature.Equals("min-width", StringComparison.OrdinalIgnoreCase)) { isMin = true; isWidth = true; }
        else if (feature.Equals("max-width", StringComparison.OrdinalIgnoreCase)) { isMin = false; isWidth = true; }
        else if (feature.Equals("min-height", StringComparison.OrdinalIgnoreCase)) { isMin = true; isWidth = false; }
        else if (feature.Equals("max-height", StringComparison.OrdinalIgnoreCase)) { isMin = false; isWidth = false; }
        else return false;

        var value = clause.AsSpan(colon + 1).Trim(" )".AsSpan());
        var digits = 0;
        while (digits < value.Length && char.IsAsciiDigit(value[digits]))
            digits++;

        return digits > 0 && int.TryParse(value[..digits], out pixels);
    }
}
