namespace SimpleCrawler.Js.Models;

/// <summary>
/// External marks a script fetched from a stable URL (cacheable: its parsed form is reused across pages).
/// An inline script's Src is the page URL, which is not a unique source key, so it is never cached.
/// <para>
/// RawSrc is the <c>src</c> attribute exactly as the markup authored it — usually relative — where Src is that
/// value resolved against the document base. Both are needed and they are not interchangeable: Src identifies
/// the source to fetch and to cache the parsed form under, while RawSrc is what
/// <c>document.currentScript.getAttribute("src")</c> must hand back, because that is what a browser returns
/// and chunk runtimes parse it as a literal (see HTMLScriptElement.src). Empty for an inline script.
/// </para>
/// <para>
/// Deferred marks a script the parser did not run where it sat — <c>async</c> or <c>defer</c> on an external
/// classic script. The fetch cannot complete before the parser reaches the next inline script, so a browser
/// always runs the rest of the document's inline code first, and page code is written expecting that.
/// </para>
/// <para>
/// Index is the script's position in the document's script order, which names the element back to the DOM so
/// <c>document.currentScript</c> is the page's own tag — the one carrying the data-* attributes a widget
/// reads its configuration from. -1 for a script the collector did not report.
/// </para>
/// </summary>
internal readonly record struct RegularScript(string Source, string Src, string RawSrc, bool External, bool Deferred, int Index);
