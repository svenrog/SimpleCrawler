using Crawler.Js.Parsing;
using HtmlAgilityPack;

namespace Crawler.Js.HtmlAgilityPack;

// Walks HAP's parsed tree into the flat, parent-indexed IR that dom.js consumes. HAP decodes entities in
// InnerText and walks cleanly; names are lowercased to match dom.js's attribute/tag conventions.
public sealed class HtmlAgilityPackHtmlParser : IHtmlParser
{
    public ParsedDocument Parse(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html ?? string.Empty);

        var root = document.DocumentNode.Element("html") ?? document.DocumentNode;
        var nodes = new List<ParsedNode>();
        Walk(root, -1, nodes);
        return new ParsedDocument(nodes);
    }

    private static void Walk(HtmlNode node, int parentIndex, List<ParsedNode> nodes)
    {
        var selfIndex = parentIndex;
        if (Project(node, parentIndex) is { } parsed)
        {
            selfIndex = nodes.Count;
            nodes.Add(parsed);
        }

        var children = node.ChildNodes;
        for (var i = 0; i < children.Count; i++)
        {
            Walk(children[i], selfIndex, nodes);
        }
    }

    private static ParsedNode? Project(HtmlNode node, int parentIndex)
    {
        if (node.NodeType == HtmlNodeType.Element)
        {
            var attrs = new List<KeyValuePair<string, string>>();
            foreach (HtmlAttribute attr in node.Attributes)
            {
                attrs.Add(new(attr.Name.ToLowerInvariant(), attr.Value));
            }

            return new ParsedNode(ParsedNodeKind.Element, node.Name.ToLowerInvariant(), string.Empty, attrs, parentIndex);
        }

        if (node.NodeType == HtmlNodeType.Text)
        {
            return new ParsedNode(ParsedNodeKind.Text, string.Empty, HtmlEntity.DeEntitize(node.InnerText), [], parentIndex);
        }

        if (node.NodeType == HtmlNodeType.Comment)
        {
            return new ParsedNode(ParsedNodeKind.Comment, string.Empty, CommentData(node), [], parentIndex);
        }

        return null;
    }

    private static string CommentData(HtmlNode node)
    {
        var outer = node.OuterHtml ?? string.Empty;
        if (outer.StartsWith("<!--", StringComparison.Ordinal) && outer.Length >= 7 && outer.EndsWith("-->", StringComparison.Ordinal))
        {
            return outer.Substring(4, outer.Length - 7);
        }

        return outer;
    }
}
