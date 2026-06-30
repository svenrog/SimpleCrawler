using Crawler.Js.AngleSharp;
using Crawler.Js.HtmlAgilityPack;
using Crawler.Js.Parsing;

namespace Crawler.Tests;

// Both native parsers must produce the same flat, parent-indexed IR the JS tree builder consumes: html root,
// lowercased tags/attrs, decoded text, a surviving comment, and valid pre-order parent indices.
public class NativeHtmlParserTests
{
    private const string Html = """
        <!doctype html><html><head><title>T</title><link rel="canonical" href="/canon" /></head>
        <body class="b"><a href="/a">A</a><p>para &amp; more</p><!--note--></body></html>
        """;

    [Fact]
    public void AngleSharp_BuildsExpectedTree()
        => AssertTree(new AngleSharpHtmlParser().Parse(Html));

    [Fact]
    public void HtmlAgilityPack_BuildsExpectedTree()
        => AssertTree(new HtmlAgilityPackHtmlParser().Parse(Html));

    private static void AssertTree(ParsedDocument document)
    {
        var nodes = document.Nodes;

        Assert.NotEmpty(nodes);
        Assert.Equal(ParsedNodeKind.Element, nodes[0].Kind);
        Assert.Equal("html", nodes[0].Tag);
        Assert.Equal(-1, nodes[0].ParentIndex);

        var elements = nodes.Where(n => n.Kind == ParsedNodeKind.Element).Select(n => n.Tag).ToArray();
        Assert.Contains("head", elements);
        Assert.Contains("body", elements);
        Assert.Contains("a", elements);
        Assert.Contains("link", elements);

        var body = nodes.First(n => n.Kind == ParsedNodeKind.Element && n.Tag == "body");
        Assert.Contains(new KeyValuePair<string, string>("class", "b"), body.Attributes);

        var anchor = nodes.First(n => n.Kind == ParsedNodeKind.Element && n.Tag == "a");
        Assert.Equal("/a", anchor.Attributes.First(kv => kv.Key == "href").Value);

        Assert.Contains(nodes, n => n.Kind == ParsedNodeKind.Text && n.Data.Contains("para & more"));
        Assert.Contains(nodes, n => n.Kind == ParsedNodeKind.Comment && n.Data.Contains("note"));

        for (var i = 1; i < nodes.Count; i++)
        {
            Assert.InRange(nodes[i].ParentIndex, 0, i - 1);
        }
    }
}
