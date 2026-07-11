using Microsoft.Extensions.Logging;

namespace SimpleCrawler.Js.Models;

public class JsRenderOptions
{
    public int MaxTaskDrainIterations { get; set; } = 1000;

    /// <summary>
    /// Off by default: gives the bundle a real network-backed fetch/XMLHttpRequest so SPAs that load
    /// their content (and links) at runtime can render. It issues live HTTP requests per page during
    /// rendering, so it is opt-in rather than a default cost.
    /// </summary>
    public bool EnableFetch { get; set; }

    /// <summary>
    /// Off by default: installs an in-memory <c>window.indexedDB</c> so sites that gate a runtime data
    /// cache on its presence take their caching path instead of re-requesting every drain turn.
    /// </summary>
    public bool EnableIndexedDb { get; set; }

    /// <summary>
    /// Off by default: installs a WHATWG Streams shim (<c>ReadableStream</c>, <c>TransformStream</c>,
    /// <c>TextDecoderStream</c>, …) and gives <c>Response.body</c> a readable stream. Bodies are
    /// buffered-complete (the fetch path already materializes the whole response), so this delivers
    /// spec-compliant reader/transform semantics rather than incremental transport streaming.
    /// A streaming/hydration bundle (e.g. Next.js App Router RSC) may run its streaming path once this is
    /// on and, in the single-pass render, tear down the server markup without rebuilding it; a baseline
    /// guard restores the pre-script tree if the render would otherwise regress below the shell's links.
    /// </summary>
    public bool EnableStreams { get; set; }

    /// <summary>
    /// Off by default: makes <c>canvas.getContext("webgl"|"webgl2")</c> return a non-faulting stub context
    /// instead of <c>null</c>. Map/3D libraries (Mapbox GL, Three.js, deck.gl) initialize WebGL synchronously
    /// while constructing and throw "Failed to initialize WebGL." on a null context — an uncaught throw that
    /// trips the SPA error boundary and drops every link on the page. The stub reports success through setup so
    /// the library finishes; nothing is drawn (the map yields no anchors anyway) but the surrounding page
    /// renders. Opt-in because, once initialized, such a library may begin fetching map tiles/style.
    /// </summary>
    public bool EnableWebGl { get; set; }

    /// <summary>
    /// Determines the dimensions of the window that should be communicated to the scripts
    /// 1920x1080 is the default window size
    /// </summary>
    public Viewport Viewport { get; set; } = Viewport.Default;

    /// <summary>
    /// Determines if and which script logging level should be output to the log stream.
    /// </summary>
    public LogLevel? ScriptLogging { get; set; }
}
