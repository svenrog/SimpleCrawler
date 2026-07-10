using SimpleCrawler.Js.Helpers;

namespace SimpleCrawler.Js.Rendering;

internal static class JsPreludes
{
    /// <summary>
    /// The self-contained pure-JS DOM (parser + node tree + globals). It owns document/window/navigation
    /// so the bundle never crosses into managed wrappers.
    /// </summary>
    public static readonly PreludeEntry Dom = Load("dom.js");

    /// <summary>
    /// Network fetch/XHR shim, run only when EnableFetch is on.
    /// </summary>
    public static readonly PreludeEntry Fetch = Load("fetch.js");

    /// <summary>
    /// WHATWG Streams shim (ReadableStream/TransformStream/…), run only when EnableStreams is on. Kept out
    /// of dom.js so the default render neither evaluates it nor exposes the stream globals.
    /// </summary>
    public static readonly PreludeEntry Stream = Load("stream.js");

    /// <summary>
    /// In-memory IndexedDB, run only when EnableIndexedDb is on. Kept out of dom.js so the default render
    /// neither evaluates its implementation nor exposes window.indexedDB.
    /// </summary>
    public static readonly PreludeEntry IndexedDb = Load("indexeddb.js");

    private static PreludeEntry Load(string fileName)
    {
        var type = typeof(JsPreludes);
        return PreludeHelper.Load(type, fileName);
    }
}
