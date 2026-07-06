namespace SimpleCrawler.Js.Models;

// External marks a module fetched from a stable URL (cacheable: its parsed form is reused across pages).
// An inline module's Specifier is the page URL, which is unique per page, so it is never cached — caching
// it would retain one parsed AST per crawled page for the whole crawl.
internal readonly record struct ModuleScript(string Specifier, string Source, bool External);
