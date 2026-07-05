namespace Crawler.Core.Proxy;

public sealed class PreparedProxyResolver : IProxyResolver
{
    public IReadOnlyCollection<ProxyInfo> Resolve(IEnumerable<string> proxies)
    {
        return [.. proxies.Select(ProxyParser.Parse).Where(i => i.Protocol != ProxyProtocol.Unknown)];
    }
}
