using SimpleCrawler.Core.Models;
using SimpleCrawler.HtmlAgilityPack;
using SimpleCrawler.Tests.Assertions;
using SimpleCrawler.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleCrawler.Tests;

[Collection("Crawler")]
public class RobotsTests : IClassFixture<RobotsHostFixture>
{
    private readonly RobotsHostFixture _context;

    public RobotsTests(RobotsHostFixture hostFixture)
    {
        _context = hostFixture;
    }

    [Fact]
    public async Task HtmlAgilityPackCrawler_Can_Crawl_Using_Sitemap()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultHtmlAgilityPackCrawler>();
        var result = await subject.Start(RobotsHostFixture.HostName, TestContext.Current.CancellationToken);

        AssertResult(result);
    }

    protected void AssertResult(IScrapeResult result)
    {
        LinkAssertions.AssertSameLinks(_context.Links, result.Urls);
    }
}
