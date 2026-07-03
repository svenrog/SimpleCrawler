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

    /// <summary>
    /// Reusing a pooled engine across pages requires resetting Jint's internal ES-module registry and global
    /// lexical record between pages, for which Jint exposes no public API — <see cref="JintRealmReset"/> reaches
    /// them reflectively. That reflection does not survive trimming / NativeAOT, so set this to <c>false</c> for
    /// those builds: engine reuse is then disabled and a fresh engine is built per page (equivalent to
    /// <see cref="MaxUsesPerEngine"/> = 0), which needs no reset and touches no reflection. Reuse is also
    /// disabled automatically if a future Jint release renames the internals the reset depends on.
    /// </summary>
    public bool AllowReflectiveRealmReset { get; set; } = true;
}
