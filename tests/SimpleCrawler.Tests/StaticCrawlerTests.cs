using SimpleCrawler.AngleSharp;
using SimpleCrawler.Core.Models;
using SimpleCrawler.HtmlAgilityPack;
using SimpleCrawler.Js.Jint;
using SimpleCrawler.Js.V8;
using SimpleCrawler.Playwright;
using SimpleCrawler.Puppeteer;
using SimpleCrawler.Tests.Assertions;
using SimpleCrawler.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleCrawler.Tests;

[Collection("Crawler")]
public class StaticCrawlerTests : IClassFixture<StaticHostFixture>
{
    private readonly StaticHostFixture _context;

    public StaticCrawlerTests(StaticHostFixture hostFixture)
    {
        _context = hostFixture;
    }

    [Fact]
    public async Task HtmlAgilityPackCrawler_Can_Crawl()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultHtmlAgilityPackCrawler>();
        var result = await subject.Start(StaticHostFixture.HostName, TestContext.Current.CancellationToken);

        AssertResult(result);
    }

    [Fact]
    public async Task HtmlAgilityPackCrawler_Can_Crawl_Twice()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultHtmlAgilityPackCrawler>();
        var firstResult = await subject.Start(StaticHostFixture.HostName, TestContext.Current.CancellationToken);
        AssertResult(firstResult);

        var secondResult = await subject.Start(StaticHostFixture.HostName, TestContext.Current.CancellationToken);
        AssertResult(secondResult);
    }

    [Fact]
    public async Task V8Crawler_Can_Crawl()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultV8Crawler>();
        var result = await subject.Start(StaticHostFixture.HostName, TestContext.Current.CancellationToken);

        AssertResult(result);
    }

    [Fact]
    public async Task JintCrawler_Can_Crawl()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultJintCrawler>();
        var result = await subject.Start(StaticHostFixture.HostName, TestContext.Current.CancellationToken);

        AssertResult(result);
    }

    [Fact]
    public async Task AngleSharpCrawler_Can_Crawl()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultAngleSharpCrawler>();
        var result = await subject.Start(StaticHostFixture.HostName, TestContext.Current.CancellationToken);

        AssertResult(result);
    }

    [Fact]
    public async Task PlaywrightCrawler_Can_Crawl()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultPlaywrightCrawler>();
        var result = await subject.Start(StaticHostFixture.HostName, TestContext.Current.CancellationToken);

        AssertResult(result);
    }

    [Fact]
    public async Task PuppeteerCrawler_Can_Crawl()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultPuppeteerCrawler>();
        var result = await subject.Start(StaticHostFixture.HostName, TestContext.Current.CancellationToken);

        AssertResult(result);
    }

    protected void AssertResult(IScrapeResult result)
    {
        LinkAssertions.AssertSameLinks(_context.Links, result.Urls);
    }
}
