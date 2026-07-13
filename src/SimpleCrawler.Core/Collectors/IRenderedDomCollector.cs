using SimpleCrawler.Core.Models;
using System.Text.Json;

namespace SimpleCrawler.Core.Collectors;

public interface IRenderedDomCollector : IDomCollector
{
    /// <summary>
    /// A JavaScript expression evaluating to a zero-argument function that returns a JSON-serializable value,
    /// e.g. <c>() =&gt; ({ ... })</c>. It runs in-page on the rendered backends — a real browser under
    /// Playwright/Puppeteer, or the in-process JS DOM — so it must use only standard DOM read APIs. It runs in
    /// its own function scope and is isolated: a throw, or a result that will not serialize, yields no data for
    /// this collector without disturbing the crawl or the other collectors.
    /// </summary>
    string DomScript { get; }

    /// <summary>
    /// Rendered-backend path: consume the JSON <paramref name="result"/> that <see cref="DomScript"/> produced
    /// onto <paramref name="report"/>. Not called when the fragment produced no result.
    /// </summary>
    ValueTask OnRendered(UrlReport report, JsonElement result, string resolvedUrl);
}
