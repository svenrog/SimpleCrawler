using SimpleCrawler.Core.Retry;

namespace SimpleCrawler.Core.Proxy;

public interface IProxyPool
{
    IReadOnlyList<ProxyInfo> Proxies { get; }

    ProxyInfo? Acquire();

    void ReportSuccess(ProxyInfo proxy);

    void ReportFailure(ProxyInfo proxy, RetryReason reason);

    ProxyPoolSnapshot Snapshot();
}
