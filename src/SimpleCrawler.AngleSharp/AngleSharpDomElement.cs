using AngleSharp.Dom;
using SimpleCrawler.Core.Collectors;

namespace SimpleCrawler.AngleSharp;

/// <summary><see cref="IDomElement"/> over an AngleSharp <see cref="IElement"/>.</summary>
internal sealed class AngleSharpDomElement : IDomElement
{
    private readonly IElement _element;

    public AngleSharpDomElement(IElement element)
    {
        _element = element;
    }

    public string? GetAttribute(string name) => _element.GetAttribute(name);

    public string Text => _element.TextContent;
}
