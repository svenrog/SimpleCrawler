using Crawler.Core.Models;
using Crawler.Tests.Common.Crawlers;
using Crawler.Tests.Fixtures;
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
        var subject = _context.ServiceProvider.GetRequiredService<PlaywrightCrawler>();
        var result = await subject.Scrape(SpaHostFixture.HostName, _context.CancellationSource.Token);

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
