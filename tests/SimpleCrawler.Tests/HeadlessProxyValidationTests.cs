using SimpleCrawler.Core;
using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Playwright;
using SimpleCrawler.Puppeteer;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.Tests;

public class HeadlessProxyValidationTests
{
    private static IOptions<HeadlessCrawlerOptions> OptionsWithPool() =>
        Options.Create(new HeadlessCrawlerOptions { ProxyPool = new ProxyPoolOptions() });

    private static ProxyPool AuthenticatedSocksPool() => new(
        [new ProxyInfo { Host = "10.0.0.1", Port = 1080, Protocol = ProxyProtocol.Socks5, Username = "user", Password = "pass" }],
        new ProxyPoolOptions());

    private static ProxyPool HttpPool() => new(
        [new ProxyInfo { Host = "10.0.0.1", Port = 8080, Protocol = ProxyProtocol.Http }],
        new ProxyPoolOptions());

    [Fact]
    public void Playwright_Session_Fails_Fast_On_Authenticated_Socks()
    {
        Assert.Throws<NotSupportedException>(() => new PlaywrightBrowserSession(OptionsWithPool(), AuthenticatedSocksPool()));
    }

    [Fact]
    public void Puppeteer_Session_Fails_Fast_On_Authenticated_Socks()
    {
        Assert.Throws<NotSupportedException>(() => new PuppeteerBrowserSession(OptionsWithPool(), AuthenticatedSocksPool()));
    }

    [Fact]
    public void Playwright_Session_Accepts_Supported_Proxies()
    {
        _ = new PlaywrightBrowserSession(OptionsWithPool(), HttpPool());
    }

    [Fact]
    public void Puppeteer_Session_Accepts_Supported_Proxies()
    {
        _ = new PuppeteerBrowserSession(OptionsWithPool(), HttpPool());
    }
}
