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
        switch (input)
        {
            case null:
                return null;
            case byte[] bytes:
                return bytes;
            case IEnumerable<object?> items:
                return items.Select(Convert.ToByte).ToArray();
            default:
                return null;
        }
    }
}
