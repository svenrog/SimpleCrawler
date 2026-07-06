using Crawler.Core.Proxy;

namespace Crawler.Tests;

public class ProxyParserTests
{
    private static readonly IProxyResolver _resolver = new PreparedProxyResolver();

    private static ProxyInfo ParseOne(string input)
    {
        var resolved = _resolver.Resolve([input]);
        return resolved.Single();
    }

    [Theory]
    [InlineData("http://10.0.0.1:8080", ProxyProtocol.Http, "10.0.0.1", 8080)]
    [InlineData("https://proxy.example.com:443", ProxyProtocol.Https, "proxy.example.com", 443)]
    [InlineData("socks5://10.0.0.2:1080", ProxyProtocol.Socks5, "10.0.0.2", 1080)]
    [InlineData("socks4a://10.0.0.3:1080", ProxyProtocol.Socks4, "10.0.0.3", 1080)]
    public void Parses_Scheme_Uri(string input, ProxyProtocol protocol, string host, int port)
    {
        var info = ParseOne(input);

        Assert.Equal(protocol, info.Protocol);
        Assert.Equal(host, info.Host);
        Assert.Equal(port, info.Port);
        Assert.False(info.HasCredentials);
    }

    [Fact]
    public void Parses_Credentials_In_Uri()
    {
        var info = ParseOne("http://alice:s3cr3t@10.0.0.4:3128");

        Assert.Equal(ProxyProtocol.Http, info.Protocol);
        Assert.Equal("10.0.0.4", info.Host);
        Assert.Equal(3128, info.Port);
        Assert.True(info.HasCredentials);
        Assert.Equal("alice", info.Username);
        Assert.Equal("s3cr3t", info.Password);
    }

    [Fact]
    public void Parses_Schemeless_HostPort_As_Http()
    {
        var info = ParseOne("10.0.0.5:8080");

        Assert.Equal(ProxyProtocol.Http, info.Protocol);
        Assert.Equal("10.0.0.5", info.Host);
        Assert.Equal(8080, info.Port);
    }

    [Fact]
    public void Parses_Schemeless_ColonForm_Credentials()
    {
        var info = ParseOne("10.0.0.6:8080:bob:pass");

        Assert.Equal(ProxyProtocol.Http, info.Protocol);
        Assert.Equal(8080, info.Port);
        Assert.Equal("bob", info.Username);
        Assert.Equal("pass", info.Password);
    }

    [Fact]
    public void Parses_Userinfo_Form_Credentials()
    {
        var info = ParseOne("carol:hunter2@10.0.0.7:8080");

        Assert.Equal(ProxyProtocol.Http, info.Protocol);
        Assert.Equal("carol", info.Username);
        Assert.Equal("hunter2", info.Password);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("10.0.0.8:notaport")]
    [InlineData("://nohost")]
    [InlineData("   ")]
    public void Drops_Unparseable_Lines(string input)
    {
        var resolved = _resolver.Resolve([input]);

        Assert.Empty(resolved);
    }

    [Fact]
    public void Keeps_Only_Valid_From_Mixed_List()
    {
        var resolved = _resolver.Resolve(
        [
            "http://10.0.0.9:8080",
            "not a proxy",
            "socks5://10.0.0.10:1080",
        ]);

        Assert.Equal(2, resolved.Count);
    }
}
