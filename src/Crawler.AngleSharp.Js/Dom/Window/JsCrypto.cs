namespace Crawler.AngleSharp.Js.Dom.Window;

public sealed class JsCrypto
{
    public string randomUUID() => Guid.NewGuid().ToString();
}
