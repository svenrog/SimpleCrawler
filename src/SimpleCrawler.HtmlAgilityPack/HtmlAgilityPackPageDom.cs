using HtmlAgilityPack;
using SimpleCrawler.Core.Collectors;

namespace SimpleCrawler.HtmlAgilityPack;

/// <summary>
/// <see cref="IPageDom"/> over a parsed HtmlAgilityPack <see cref="HtmlDocument"/>. Elements are indexed by
/// tag name in one explicit-stack walk on construction — HtmlAgilityPack's <c>Descendants()</c> regresses
/// allocation here — so each <see cref="QueryAll"/> is a dictionary lookup. Built only when a DOM collector
/// is registered.
/// </summary>
internal sealed class HtmlAgilityPackPageDom : IPageDom
{
    private static readonly IReadOnlyList<IDomElement> _none = [];

    private readonly Dictionary<string, List<IDomElement>> _byTag;

    public HtmlAgilityPackPageDom(HtmlDocument document)
    {
        _byTag = new Dictionary<string, List<IDomElement>>(StringComparer.OrdinalIgnoreCase);

        var stack = new Stack<HtmlNode>();
        stack.Push(document.DocumentNode);

        while (stack.Count > 0)
        {
            var node = stack.Pop();

            var children = node.ChildNodes;
            for (var i = children.Count - 1; i >= 0; i--)
                stack.Push(children[i]);

            if (node.NodeType != HtmlNodeType.Element)
                continue;

            if (!_byTag.TryGetValue(node.Name, out var list))
                _byTag[node.Name] = list = [];

            list.Add(new HtmlAgilityPackDomElement(node));
        }
    }

    public IReadOnlyList<IDomElement> QueryAll(string localName)
        => _byTag.TryGetValue(localName, out var list) ? list : _none;
}
