using Microsoft.Extensions.Logging;

namespace Crawler.AngleSharp.Js.Models;

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
    /// Off by default: lets the bundle store expando properties on DOM nodes (React 18 stashes fibers there).
    /// It swaps the plain node wrappers for dynamic ones (IDynamicMetaObjectProvider + a side table), which
    /// routes member access through the DLR and is slower — so it is opt-in. Effective on the Jint backend;
    /// ClearScript/V8 only honours DynamicObject (not a custom meta-object), so it is a no-op there for now.
    /// </summary>
    public bool EnableDomExpandos { get; set; }

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
