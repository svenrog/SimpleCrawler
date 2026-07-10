using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimpleCrawler.Core.Collections;

/// <summary>
/// Serializes a ConcurrentHashSet&lt;string&gt; as a plain JSON array. Written by hand (no reflection) so it
/// stays trim/AOT-safe and usable from a source-generated JsonSerializerContext.
/// </summary>
public sealed class ConcurrentHashSetJsonConverter : JsonConverter<ConcurrentHashSet<string>>
{
    public override ConcurrentHashSet<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected an array.");

        var set = new ConcurrentHashSet<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            set.Add(reader.GetString()!);

        return set;
    }

    public override void Write(Utf8JsonWriter writer, ConcurrentHashSet<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
            writer.WriteStringValue(item);
        writer.WriteEndArray();
    }
}
