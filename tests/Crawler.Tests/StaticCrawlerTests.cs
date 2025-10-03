using Crawler.Core.Models;
using Crawler.Tests.Common.Crawlers;
using Crawler.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Crawler.Tests;

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
        var subject = _context.ServiceProvider.GetRequiredService<HtmlAgilityPackCrawler>();
        var result = await subject.Scrape(_context.CancellationSource.Token);

        AssertResult(result);
    }

    [Fact]
    public async Task AngleSharpCrawler_Can_Crawl()
    {
        var subject = _context.ServiceProvider.GetRequiredService<AngleSharpCrawler>();
        var result = await subject.Scrape(_context.CancellationSource.Token);

        AssertResult(result);
    }

    [Fact]
    public async Task PlaywrightCrawler_Can_Crawl()
    {
        var subject = _context.ServiceProvider.GetRequiredService<PlaywrightCrawler>();
        var result = await subject.Scrape(_context.CancellationSource.Token);

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
