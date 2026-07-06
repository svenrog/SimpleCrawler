namespace Crawler.Core.Proxy;

public interface IProxyClientProvider
{
    HttpClient ClientFor(ProxyInfo proxy);
}
