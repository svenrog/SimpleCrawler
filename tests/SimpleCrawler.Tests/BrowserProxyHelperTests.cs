using SimpleCrawler.Core.Proxy;

namespace SimpleCrawler.Tests;

public class BrowserProxyHelperTests
{
    private static ProxyInfo Proxy(ProxyProtocol protocol, bool credentials) => new()
    {
        Host = "10.0.0.1",
        Port = 1080,
        Protocol = protocol,
        Username = credentials ? "user" : null,
        Password = credentials ? "pass" : null,
    };

    [Theory]
    [InlineData(ProxyProtocol.Http, true, true)]
    [InlineData(ProxyProtocol.Https, true, true)]
    [InlineData(ProxyProtocol.Socks5, false, true)]
    [InlineData(ProxyProtocol.Socks4, false, true)]
    [InlineData(ProxyProtocol.Socks5, true, false)]
    [InlineData(ProxyProtocol.Socks4, true, false)]
    public void IsSupported_Rejects_Only_Authenticated_Socks(ProxyProtocol protocol, bool credentials, bool expected)
    {
        Assert.Equal(expected, BrowserProxyHelper.IsSupported(Proxy(protocol, credentials)));
    }

    [Fact]
    public void EnsureSupported_Throws_For_Authenticated_Socks()
    {
        Assert.Throws<NotSupportedException>(() => BrowserProxyHelper.EnsureSupported(Proxy(ProxyProtocol.Socks5, credentials: true)));
    }

    [Fact]
    public void EnsureAllSupported_Throws_When_Any_Is_Authenticated_Socks()
    {
        var proxies = new[]
        {
            Proxy(ProxyProtocol.Http, credentials: true),
            Proxy(ProxyProtocol.Socks5, credentials: true),
        };

        Assert.Throws<NotSupportedException>(() => BrowserProxyHelper.EnsureAllSupported(proxies));
    }

    [Theory]
    [InlineData(ProxyProtocol.Http, "http://10.0.0.1:1080")]
    [InlineData(ProxyProtocol.Https, "https://10.0.0.1:1080")]
    [InlineData(ProxyProtocol.Socks5, "socks5://10.0.0.1:1080")]
    [InlineData(ProxyProtocol.Socks4, "socks4://10.0.0.1:1080")]
    public void ToServerArg_Builds_Scheme_Host_Port(ProxyProtocol protocol, string expected)
    {
        Assert.Equal(expected, BrowserProxyHelper.ToServerArg(Proxy(protocol, credentials: false)));
    }

    [Fact]
    public void ContextKey_Is_Empty_For_Null()
    {
        Assert.Equal(string.Empty, BrowserProxyHelper.ContextKey(null));
        Assert.Equal("http://10.0.0.1:1080", BrowserProxyHelper.ContextKey(Proxy(ProxyProtocol.Http, credentials: false)));
    }
}
