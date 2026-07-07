namespace SimpleCrawler.Core.Proxy;

public static class BrowserProxyHelper
{
    public static bool IsSupported(ProxyInfo proxy) => !(IsSocks(proxy.Protocol) && proxy.HasCredentials);

    public static void EnsureSupported(ProxyInfo proxy)
    {
        if (!IsSupported(proxy))
            throw new NotSupportedException(
                $"Authenticated SOCKS proxies cannot be used by the headless backends: Chromium does not support SOCKS proxy authentication. Offending proxy: {proxy.Host}:{proxy.Port}. Use an HTTP(S) proxy, an unauthenticated SOCKS proxy, or a static (HttpClient) backend.");
    }

    public static void EnsureAllSupported(IEnumerable<ProxyInfo> proxies)
    {
        foreach (var proxy in proxies)
            EnsureSupported(proxy);
    }

    public static string ToServerArg(ProxyInfo proxy) => $"{Scheme(proxy.Protocol)}://{proxy.Host}:{proxy.Port}";

    public static string ContextKey(ProxyInfo? proxy) => proxy is null ? string.Empty : ToServerArg(proxy);

    private static bool IsSocks(ProxyProtocol protocol) => protocol is ProxyProtocol.Socks4 or ProxyProtocol.Socks5;

    private static string Scheme(ProxyProtocol protocol) => protocol switch
    {
        ProxyProtocol.Http => "http",
        ProxyProtocol.Https => "https",
        ProxyProtocol.Socks4 => "socks4",
        ProxyProtocol.Socks5 => "socks5",
        _ => "http",
    };
}
