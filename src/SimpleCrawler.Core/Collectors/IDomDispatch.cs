using SimpleCrawler.Core.Models;

namespace SimpleCrawler.Core.Collectors;

/// <summary>
/// The DOM material a backend produced for one page, in a form that can feed any registered
/// <see cref="IDomCollector"/> without the core pipeline knowing which backend family produced it. A static
/// backend supplies a dispatch backed by an <see cref="IPageDom"/>; a rendered backend one backed by the
/// per-collector JSON slices of its in-page extraction.
/// </summary>
public interface IDomDispatch
{
    /// <summary>Feeds this page's DOM material for <paramref name="collector"/> to that collector.</summary>
    ValueTask Dispatch(UrlReport report, IDomCollector collector, string resolvedUrl);
}
