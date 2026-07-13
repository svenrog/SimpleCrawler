namespace SimpleCrawler.Core.Collectors;

/// <summary>
/// A backend-neutral, read-only view of one parsed DOM element, exposing only what a collector needs to
/// inspect it — its attributes and text — over whichever native node the static backend produced. It lets
/// an <see cref="IDomCollector"/> extract data without depending on a specific HTML library.
/// </summary>
public interface IDomElement
{
    /// <summary>The value of the <paramref name="name"/> attribute, or <c>null</c> when it is absent.</summary>
    string? GetAttribute(string name);

    /// <summary>The element's text content — its descendant text concatenated.</summary>
    string Text { get; }
}
