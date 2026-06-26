namespace Crawler.AngleSharp.Js.Dom;

public sealed class JsCrypto
{
    public string randomUUID() => Guid.NewGuid().ToString();
}
