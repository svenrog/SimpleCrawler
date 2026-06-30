using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Crawler.Js.Parsing;
using HtmlParser = AngleSharp.Html.Parser.HtmlParser;

namespace Crawler.Js.AngleSharp;

// Walks AngleSharp's parsed tree into the flat, parent-indexed IR that dom.js consumes. AngleSharp does full
// HTML5 parsing (entity decoding, implied structure, template .content) and lowercases names, matching what
// dom.js's collectors and the bundle expect.
public sealed class AngleSharpHtmlParser : IHtmlParser
{
    // AngleSharp's HtmlParser holds stateful tokenizer state, so a single instance cannot be shared across the
    // crawler's concurrent render threads — build a fresh one per parse.
    public ParsedDocument Parse(string html)
    {
        var document = new HtmlParser().ParseDocument(html ?? string.Empty);
        var root = document.DocumentElement;
        var nodes = new List<ParsedNode>();
        if (root is not null)
        {
            Walk(root, -1, nodes);
        }

        return new ParsedDocument(nodes);
    }

    private static void Walk(INode node, int parentIndex, List<ParsedNode> nodes)
    {
        var selfIndex = parentIndex;
        if (Project(node, parentIndex) is { } parsed)
        {
            selfIndex = nodes.Count;
            nodes.Add(parsed);
        }

        // A <template>'s children live in its inert .content fragment; emit them as the element's children so
        // the tree matches what a naive childNodes walk (and dom.js's parser) would surface.
        var children = node is IHtmlTemplateElement template ? template.Content.ChildNodes : node.ChildNodes;
        for (var i = 0; i < children.Length; i++)
        {
            Walk(children[i]!, selfIndex, nodes);
        }
    }

    private static ParsedNode? Project(INode node, int parentIndex)
    {
        if (node is IElement element)
        {
            var attrs = new List<KeyValuePair<string, string>>();
            foreach (var attr in element.Attributes)
            {
                attrs.Add(new(attr.Name, attr.Value));
            }

            return new ParsedNode(ParsedNodeKind.Element, element.LocalName, string.Empty, attrs, parentIndex);
        }

        if (node is ICharacterData characterData)
        {
            var kind = node.NodeType == NodeType.Comment ? ParsedNodeKind.Comment : ParsedNodeKind.Text;
            return new ParsedNode(kind, string.Empty, characterData.Data, [], parentIndex);
        }

        return null;
    }
}
