using SimpleCrawler.Core.Models;

namespace SimpleCrawler.Core.Collectors;

/// <summary>
/// The extension seam for per-page data capture. An implementation observes every fetched page at two
/// pipeline stages and records whatever it derives — onto the page's <see cref="UrlReport"/>, or into
/// any external sink it owns. Register one via DI (see <see cref="CollectorServiceCollectionExtensions.AddCrawlCollectors"/>)
/// and it runs for every backend — static, JS, or headless — with no change to the core pipeline or the backends. 
/// The built-in <see cref="PageSignalsCollector"/> is one such implementation.
/// </summary>
public interface ICrawlCollector
{
    /// <summary>
    /// Fetch stage: called once the normalized HTTP <paramref name="response"/> is known, before the
    /// body is parsed. This is the only stage with access to response headers and cookie names.
    /// </summary>
    void OnResponse(UrlReport report, ResponseSignal response);

    /// <summary>
    /// Parse stage: called after links, canonical, meta-robots (and any opt-in DOM signals) have been
    /// extracted into <paramref name="extract"/>, with the page's resolved canonical URL.
    /// </summary>
    ValueTask OnDocument(UrlReport report, PageExtract extract, string resolvedUrl);
}
