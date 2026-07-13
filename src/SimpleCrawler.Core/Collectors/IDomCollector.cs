namespace SimpleCrawler.Core.Collectors;

/// <summary>
/// An <see cref="ICrawlCollector"/> that also derives data from the page's DOM. Because there is no shared
/// C# DOM across backends, this is an abstraction layer for <see cref="IStaticDomCollector"/> and
/// <see cref="IRenderedDomCollector"/>
/// </summary>
public interface IDomCollector : ICrawlCollector
{
    /// <summary>
    /// A stable identifier for this collector, unique among registered collectors. It keys the collector's
    /// slice of the rendered backends' extraction envelope, so results route back to the right collector
    /// regardless of registration order.
    /// </summary>
    string Key { get; }
}