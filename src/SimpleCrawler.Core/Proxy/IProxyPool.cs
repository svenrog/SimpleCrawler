namespace SimpleCrawler.Core.Proxy;

public interface IProxyPool
{
    IReadOnlyList<ProxyInfo> Proxies { get; }

    ProxyInfo? Acquire();

    void ReportSuccess(ProxyInfo proxy);

    void ReportFailure(ProxyInfo proxy, ProxyFailureKind kind);

    ProxyPoolSnapshot Snapshot();
}
