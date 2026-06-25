using Crawler.AngleSharp.Js.Jint;
using Crawler.AngleSharp.Js.V8;
using Crawler.Core.Models;
using Crawler.Playwright;
using Crawler.Puppeteer;
using Crawler.Tests.Fixtures;
using Crawler.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Crawler.Tests;

[Collection("Crawler")]
public class SpaCrawlerTests : IClassFixture<SpaHostFixture>
{
    private readonly SpaHostFixture _context;

    public SpaCrawlerTests(SpaHostFixture hostFixture)
    {
        _context = hostFixture;
    }

    [Fact]
    public async Task PlaywrightCrawler_Can_Crawl()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultPlaywrightCrawler>();
        var result = await subject.Start(SpaHostFixture.HostName, _context.CancellationSource.Token);

        AssertResult(result);
    }

    [Fact]
    public async Task PuppeteerCrawler_Can_Crawl()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultPuppeteerCrawler>();
        var result = await subject.Start(SpaHostFixture.HostName, _context.CancellationSource.Token);

        AssertResult(result);
    }

    [Fact]
    public async Task AngleSharpJintCrawler_can_Crawl()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultAngleSharpJintCrawler>();
        var result = await subject.Start(SpaHostFixture.HostName, _context.CancellationSource.Token);

        AssertResult(result);
    }

    [Fact]
    public async Task AngleSharpV8Crawler_can_Crawl()
    {
        Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var subject = _context.ServiceProvider.GetRequiredService<DefaultAngleSharpV8Crawler>();
        var result = await subject.Start(SpaHostFixture.HostName, _context.CancellationSource.Token);

        AssertResult(result);
    }


    protected void AssertResult(IScrapeResult result)
    {
        Assert.Equal(_context.Links.Count, result.Urls.Count);

        var firstNotSecond = _context.Links.Except(result.Urls).ToList();
        Assert.Empty(firstNotSecond);

        var secondNotFirst = result.Urls.Except(_context.Links).ToList();
        Assert.Empty(secondNotFirst);
    }
}
