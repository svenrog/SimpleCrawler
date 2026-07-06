using System.Buffers;
using System.Text;
using System.Text.Json;

namespace SimpleCrawler.Core.Helpers;

// Reflection-free JSON encoding for values embedded into generated JS source. JsonSerializer.Serialize<T>
// resolves a contract for T through reflection (a trimming / NativeAOT hazard); Utf8JsonWriter's primitive
// writes carry no reflection and produce the same output. The default encoder escapes non-ASCII, so the
// results are valid JS string/array literals as well as valid JSON.
public static class JsonLiteral
{
    public static string String(string? value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
            writer.WriteStringValue(value);

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    public static string StringArray(IEnumerable<string?> values)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var value in values)
                writer.WriteStringValue(value);

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
