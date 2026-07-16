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
/// </summary>
internal readonly record struct RegularScript(string Source, string Src, string RawSrc, bool External);
