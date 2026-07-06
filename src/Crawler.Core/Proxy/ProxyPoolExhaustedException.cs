namespace Crawler.Core.Proxy;

public sealed class ProxyPoolExhaustedException : Exception
{
    public ProxyPoolExhaustedException(string message)
        : base(message)
    {
    }
}
