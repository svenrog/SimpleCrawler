using HtmlAgilityPack;
using SimpleCrawler.Core.Collectors;

namespace SimpleCrawler.HtmlAgilityPack;

/// <summary><see cref="IDomElement"/> over an HtmlAgilityPack <see cref="HtmlNode"/>.</summary>
internal sealed class HtmlAgilityPackDomElement : IDomElement
{
    private readonly HtmlNode _node;

    public HtmlAgilityPackDomElement(HtmlNode node)
    {
        _node = node;
    }

    public string? GetAttribute(string name) => _node.Attributes[name]?.Value;

    public string Text => _node.InnerText;
}
