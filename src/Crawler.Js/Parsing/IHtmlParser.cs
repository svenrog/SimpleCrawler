namespace Crawler.Js.Parsing;

// Produces a pre-order, parent-indexed node tree from an HTML string. When registered, the renderer feeds
// the serialized tree to dom.js via __crawlerLoadTree so the JS tokenizer is bypassed; when no parser is
// registered, the renderer falls back to dom.js's own __crawlerLoadHtml.
public interface IHtmlParser
{
    ParsedDocument Parse(string html);
}
