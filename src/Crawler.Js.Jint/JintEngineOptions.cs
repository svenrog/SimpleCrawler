namespace Crawler.Js.Jint;

public sealed class JintEngineOptions
{
    /// <summary>
    /// Per-Jint-engine cap in number of pages; when an engine has rendered this many pages it is disposed and
    /// a fresh one is built. Reusing an engine amortizes the realm construction and the ~90KB dom.js re-eval
    /// (the "setupGlobals" cost) across pages, resetting only per-page state in between. 0 disables reuse (a
    /// fresh engine per page), which reproduces the un-pooled behaviour. A reused realm carries a small risk of
    /// cross-page state leaking despite the reset, so it is capped rather than reused indefinitely.
    /// </summary>
    public int MaxUsesPerEngine { get; set; } = 50;
}
