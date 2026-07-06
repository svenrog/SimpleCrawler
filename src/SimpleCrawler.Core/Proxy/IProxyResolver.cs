namespace SimpleCrawler.Core.Proxy;

public interface IProxyResolver
{
    IReadOnlyCollection<ProxyInfo> Resolve(IEnumerable<string> proxies);
}