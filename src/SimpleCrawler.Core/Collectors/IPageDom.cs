namespace SimpleCrawler.Core.Collectors;

/// <summary>
/// A backend-neutral, read-only handle to a parsed static document, handed to an <see cref="IDomCollector"/>
/// so it can pull out whatever it needs without the backend knowing what that is. The static backends
/// (AngleSharp, HtmlAgilityPack) implement it over their own parse tree; the rendered backends have no C#
/// tree and instead run the collector's <see cref="IRenderedDomCollector.DomScript"/> in-page.
/// </summary>
public interface IPageDom
{
    /// <summary>
    /// Every element whose lower-cased tag name equals <paramref name="localName"/>, in document order.
    /// </summary>
    IReadOnlyList<IDomElement> QueryAll(string localName);
}
