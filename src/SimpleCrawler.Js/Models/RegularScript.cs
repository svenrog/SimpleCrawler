namespace SimpleCrawler.Js.Models;

// External marks a script fetched from a stable URL (cacheable: its parsed form is reused across pages).
// An inline script's Src is the page URL, which is not a unique source key, so it is never cached.
internal readonly record struct RegularScript(string Source, string Src, bool External);
