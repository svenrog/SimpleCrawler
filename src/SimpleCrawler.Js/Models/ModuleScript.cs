namespace SimpleCrawler.Js.Models;

/// <summary>
/// External marks a module fetched from a stable URL (cacheable: its parsed form is reused across pages).
/// An inline module's Specifier is the page URL plus an ordinal fragment — unique per page and per block, so
/// it is never cached: caching it would retain one parsed AST per crawled page for the whole crawl.
/// </summary>
internal readonly record struct ModuleScript(string Specifier, string Source, bool External);
