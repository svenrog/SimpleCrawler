namespace SimpleCrawler.Core.Proxy;

/// <summary>
/// Renders a proxy for log output, collapsing the no-proxy case to a stable word so templates using
/// "via {proxy}" never dangle as "via " when the crawl runs without a pool.
/// </summary>
public static class ProxyLabel
{
    public static string Describe(ProxyInfo? proxy) => proxy?.ToString() ?? "direct connection";
}
