namespace SimpleCrawler.Core.Models;

/// <summary>
/// Optional, opt-in per-page HTTP/DOM signals captured alongside a <see cref="UrlReport"/> when the
/// crawl is run with signal capture enabled: response headers, cookie names, script sources, meta
/// tags, and JSON-LD blocks. Populated in two passes — headers/cookies at fetch time, the rest at
/// parse time — onto the same instance, so consumers should treat any individual list/dictionary as
/// possibly still empty until the report is finalized.
/// </summary>
public sealed class PageSignals
{
    /// <summary>Response headers, lower-cased keys to single joined values.</summary>
    public Dictionary<string, string> Headers { get; set; } = [];

    /// <summary>Names of cookies set via <c>Set-Cookie</c> (values are intentionally dropped).</summary>
    public List<string> CookieNames { get; set; } = [];

    /// <summary><c>src</c> values of <c>&lt;script&gt;</c> tags found in the body.</summary>
    public List<string> ScriptSources { get; set; } = [];

    /// <summary>Meta tag name/property to content, e.g. <c>generator</c> → <c>WordPress 6.4</c>.</summary>
    public Dictionary<string, string> MetaTags { get; set; } = [];

    /// <summary>
    /// Raw contents of <c>&lt;script type="application/ld+json"&gt;</c> blocks — schema.org structured
    /// data that commercial sites use to describe their organization.
    /// </summary>
    public List<string> JsonLdBlocks { get; set; } = [];
}
