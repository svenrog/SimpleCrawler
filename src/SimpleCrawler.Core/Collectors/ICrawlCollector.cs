using SimpleCrawler.Core.Models;

namespace SimpleCrawler.Core.Collectors;

/// <summary>
/// The extension seam for per-page data capture. An implementation observes every fetched page at two
/// pipeline stages and records whatever it derives — onto the page's <see cref="UrlReport"/>, or into
/// any external sink it owns. Register one via DI (see <c>AddCrawlerCollectors</c>) and it runs for
/// every backend — static, JS, or headless — with no change to the core pipeline or the backends. The
/// built-in <see cref="PageSignalsCollector"/> is one such implementation, added by
/// <c>--captureSignals</c>.
///
/// Both hooks run on crawl worker threads (fetch and parse respectively) and may run concurrently for
/// different URLs, so any shared state an implementation touches must be thread-safe. Any exception a
/// hook throws is logged and swallowed by the pipeline — a faulty collector never aborts the crawl and
/// never fails the page it was invoked for.
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
