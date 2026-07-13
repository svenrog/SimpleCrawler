using SimpleCrawler.Core.Models;

namespace SimpleCrawler.Core.Collectors;

/// <summary>
/// An <see cref="IDomCollector"/> that also derives data from the page's DOM that acts on neutral '
/// <see cref="IPageDom"/> the static backends provide (<see cref="OnDocument"/>).
/// </summary>
public interface IStaticDomCollector : IDomCollector
{
    /// <summary>
    /// Static-backend path: derive data from the parsed <paramref name="dom"/> onto <paramref name="report"/>,
    /// for the page whose resolved canonical URL is <paramref name="resolvedUrl"/>.
    /// </summary>
    ValueTask OnDocument(UrlReport report, IPageDom dom, string resolvedUrl);
}
