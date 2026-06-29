namespace Crawler.Js.Rendering;

internal static class JsPreludes
{
    // The self-contained pure-JS DOM (parser + node tree + globals). It owns document/window/navigation
    // so the bundle never crosses into managed wrappers.
    public static readonly PreludeEntry Dom = Load("dom.js");

    // Network fetch/XHR shim, run only when EnableFetch is on.
    public static readonly PreludeEntry Fetch = Load("fetch.js");

    private static PreludeEntry Load(string filename)
    {
        var type = typeof(JsPreludes);
        var resourceName = $"{type.Namespace}.Preludes.{filename}";
        using var stream = type.Assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return new PreludeEntry(filename, reader.ReadToEnd());
    }
}
