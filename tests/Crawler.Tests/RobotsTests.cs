using Crawler.Core.Models;
using Crawler.Tests.Common.Crawlers;
using Crawler.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Crawler.Tests;

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
        var subject = _context.ServiceProvider.GetRequiredService<HtmlAgilityPackCrawler>();
        var result = await subject.Start(RobotsHostFixture.HostName, _context.CancellationSource.Token);

        AssertResult(result);
    }

    protected void AssertResult(IScrapeResult result)
    {
        var firstNotSecond = _context.Links.Except(result.Urls).ToList();
        Assert.Empty(firstNotSecond);

        var secondNotFirst = result.Urls.Except(_context.Links).ToList();
        Assert.Empty(secondNotFirst);
    }
}
