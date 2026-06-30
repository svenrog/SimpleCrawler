using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Crawler.Js.Parsing;

public sealed class ParsedDocument
{
    private readonly IReadOnlyList<ParsedNode> _nodes;

    public ParsedDocument(IReadOnlyList<ParsedNode> nodes)
    {
        _nodes = nodes;
    }

    public IReadOnlyList<ParsedNode> Nodes => _nodes;

    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteJson(writer);
        }

        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }

    public void WriteJson(Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        foreach (var node in _nodes)
        {
            writer.WriteStartObject();
            writer.WriteNumber("k", (int)node.Kind);
            if (node.Kind == ParsedNodeKind.Element)
            {
                writer.WriteString("t", node.Tag);
                if (node.Attributes.Count > 0)
                {
                    writer.WriteStartArray("a");
                    foreach (var attr in node.Attributes)
                    {
                        writer.WriteStartArray();
                        writer.WriteStringValue(attr.Key);
                        writer.WriteStringValue(attr.Value);
                        writer.WriteEndArray();
                    }

                    writer.WriteEndArray();
                }
            }
            else
            {
                writer.WriteString("d", node.Data);
            }

            writer.WriteNumber("p", node.ParentIndex);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}
