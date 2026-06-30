using System.Collections.Generic;

namespace Crawler.Js.Parsing;

// A single node in the pre-order, parent-indexed tree handed to dom.js. Elements carry Tag + Attributes;
// text/comment carry Data; the unused fields are empty. ParentIndex is -1 for the document root.
public sealed record ParsedNode(ParsedNodeKind Kind, string Tag, string Data, IReadOnlyList<KeyValuePair<string, string>> Attributes, int ParentIndex);
