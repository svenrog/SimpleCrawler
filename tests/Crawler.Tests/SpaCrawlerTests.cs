using Crawler.Js.Jint;
using Crawler.Js.V8;
using Crawler.Core.Models;
using Crawler.Playwright;
using Crawler.Puppeteer;
using Crawler.Tests.Assertions;
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

    public static TheoryData<string> Frameworks()
    {
        var data = new TheoryData<string>();
        foreach (var framework in SpaHostFixture.Frameworks)
            data.Add(framework);

        return data;
    }

    [Theory]
    [MemberData(nameof(Frameworks))]
    public async Task PlaywrightCrawler_Can_Crawl(string framework)
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultPlaywrightCrawler>();
        var result = await subject.Start(SpaHostFixture.HostName(framework), _context.CancellationSource.Token);

        AssertResult(framework, result);
    }

    [Theory]
    [MemberData(nameof(Frameworks))]
    public async Task PuppeteerCrawler_Can_Crawl(string framework)
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultPuppeteerCrawler>();
        var result = await subject.Start(SpaHostFixture.HostName(framework), _context.CancellationSource.Token);

        AssertResult(framework, result);
    }

    [Theory]
    [MemberData(nameof(Frameworks))]
    public async Task AngleSharpJintCrawler_can_Crawl(string framework)
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultJintCrawler>();
        var result = await subject.Start(SpaHostFixture.HostName(framework), _context.CancellationSource.Token);

        AssertResult(framework, result);
    }

    [Theory]
    [MemberData(nameof(Frameworks))]
    public async Task AngleSharpV8Crawler_can_Crawl(string framework)
    {
        Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var subject = _context.ServiceProvider.GetRequiredService<DefaultV8Crawler>();
        var result = await subject.Start(SpaHostFixture.HostName(framework), _context.CancellationSource.Token);

        AssertResult(framework, result);
    }

    protected static void AssertResult(string framework, IScrapeResult result)
    {
        LinkAssertions.AssertSameLinks(SpaHostFixture.LinksFor(framework), result.Urls);
    }
}
