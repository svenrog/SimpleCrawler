using HtmlAgilityPack;
using SimpleCrawler.Core.Collectors;

namespace SimpleCrawler.HtmlAgilityPack;

/// <summary>
/// <see cref="IPageDom"/> over a parsed HtmlAgilityPack <see cref="HtmlDocument"/>. The first
/// <see cref="QueryAll"/> for a given tag walks the tree once with an explicit stack (HtmlAgilityPack's
/// <c>Descendants()</c> regresses allocation) and memoizes the matches, so repeated queries are a lookup —
/// but unlike an eager index built up front, elements of tags nobody asks for are never wrapped. Built only
/// when a DOM collector is registered.
/// </summary>
internal sealed class HtmlAgilityPackPageDom : IPageDom
{
    private static readonly IReadOnlyList<IDomElement> _none = [];

    private readonly HtmlDocument _document;
    private readonly Dictionary<string, IReadOnlyList<IDomElement>> _byTag;

    public HtmlAgilityPackPageDom(HtmlDocument document)
    {
        _document = document;
        _byTag = new Dictionary<string, IReadOnlyList<IDomElement>>(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<IDomElement> QueryAll(string localName)
    {
        if (_byTag.TryGetValue(localName, out var cached))
            return cached;

        var matches = new List<IDomElement>();
        var stack = new Stack<HtmlNode>();
        stack.Push(_document.DocumentNode);

        while (stack.Count > 0)
        {
            var node = stack.Pop();

            var children = node.ChildNodes;
            for (var i = children.Count - 1; i >= 0; i--)
                stack.Push(children[i]);

            if (node.NodeType != HtmlNodeType.Element)
                continue;

            if (node.Name.Equals(localName, StringComparison.OrdinalIgnoreCase))
                matches.Add(new HtmlAgilityPackDomElement(node));
        }

        var result = matches.Count > 0 ? matches : _none;
        _byTag[localName] = result;
        return result;
    }
}
