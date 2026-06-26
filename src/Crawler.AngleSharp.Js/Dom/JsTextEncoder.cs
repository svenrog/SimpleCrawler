using System.Text;

namespace Crawler.AngleSharp.Js.Dom;

public sealed class JsTextEncoder
{
    public string encoding => "utf-8";

    public byte[] encode(object? input) => Encoding.UTF8.GetBytes(input?.ToString() ?? string.Empty);
}
