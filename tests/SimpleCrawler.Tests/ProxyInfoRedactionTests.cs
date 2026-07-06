using SimpleCrawler.Core.Proxy;

namespace SimpleCrawler.Tests;

public class ProxyInfoRedactionTests
{
    private static readonly ProxyInfo _withCredentials = new()
    {
        Host = "10.0.0.1",
        Port = 8080,
        Protocol = ProxyProtocol.Http,
        Username = "secret-user",
        Password = "secret-pass",
    };

    [Fact]
    public void ToUri_Omits_Credentials()
    {
        var uri = _withCredentials.ToUri();

        Assert.Equal(string.Empty, uri.UserInfo);
        Assert.DoesNotContain("secret-user", uri.ToString());
        Assert.DoesNotContain("secret-pass", uri.ToString());
    }

    [Fact]
    public void ToString_Omits_Credentials()
    {
        var text = _withCredentials.ToString();

        Assert.Equal("http://10.0.0.1:8080/", text);
        Assert.DoesNotContain("secret-user", text);
        Assert.DoesNotContain("secret-pass", text);
    }
}
