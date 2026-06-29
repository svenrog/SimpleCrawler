namespace Crawler.AngleSharp.Js.Rendering;

internal static class JsPreludes
{
    public static readonly PreludeEntry InstanceShims = Load("instance-shims.js");
    public static readonly PreludeEntry Global = Load("global.js");
    public static readonly PreludeEntry Crypto = Load("crypto.js");
    public static readonly PreludeEntry Console = Load("console.js");
    public static readonly PreludeEntry ResourceEvent = Load("resource-event.js");
    public static readonly PreludeEntry MessageChannel = Load("message-channel.js");
    public static readonly PreludeEntry History = Load("history.js");
    public static readonly PreludeEntry HtmlElement = Load("html-element.js");
    public static readonly PreludeEntry DomGlobals = Load("dom-globals.js");
    public static readonly PreludeEntry Fetch = Load("fetch.js");

    // The self-contained pure-JS DOM (parser + node tree + globals). Js mode runs this alone; it owns
    // document/window/navigation so the bundle never crosses into managed wrappers.
    public static readonly PreludeEntry Dom = Load("dom.js");

    // The consecutive setup preludes folded into one cached script, so a fresh per-page context pays a
    // single engine boundary crossing (and single compile-cache lookup) instead of eight.
    public static readonly PreludeEntry CombinedGlobals = Combine("__combined_globals",
        Global, Crypto, Console, ResourceEvent, MessageChannel, History, HtmlElement, DomGlobals);

    private static PreludeEntry Combine(string key, params PreludeEntry[] parts) =>
        new(key, string.Join("\n;\n", parts.Select(static p => p.Source)));

    private static PreludeEntry Load(string filename)
    {
        var type = typeof(JsPreludes);
        var resourceName = $"{type.Namespace}.Preludes.{filename}";
        using var stream = type.Assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return new PreludeEntry(filename, reader.ReadToEnd());
    }
}
