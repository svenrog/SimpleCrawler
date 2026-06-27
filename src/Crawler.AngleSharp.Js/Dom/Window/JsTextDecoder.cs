using System.Text;

namespace Crawler.AngleSharp.Js.Dom.Window;

public sealed class JsTextDecoder
{
    public string encoding => "utf-8";

    public string decode(object? input = null)
    {
        var bytes = ToBytes(input);
        return bytes is null ? string.Empty : Encoding.UTF8.GetString(bytes);
    }

    private static byte[]? ToBytes(object? input)
    {
        return input switch
        {
            null => null,
            byte[] bytes => bytes,
            IEnumerable<object?> items => [.. items.Select(Convert.ToByte)],
            _ => null,
        };
    }
}
