using Crawler.AngleSharp.Js.Jint;
using Crawler.AngleSharp.Js.V8;
using Crawler.Core.Models;
using Crawler.Tests.Fixtures;
using Crawler.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Crawler.Tests;

[Collection("Crawler")]
public class FetchCrawlerTests : IClassFixture<FetchHostFixture>
{
    private readonly FetchHostFixture _context;

    public FetchCrawlerTests(FetchHostFixture hostFixture)
    {
        _context = hostFixture;
    }

    [Fact]
    public async Task AngleSharpJintCrawler_Renders_Fetch()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultAngleSharpJintCrawler>();
        var result = await subject.Start(FetchHostFixture.HostName, _context.CancellationSource.Token);

        AssertResult(result);
    }

    [Fact]
    public async Task AngleSharpV8Crawler_Renders_Fetch()
    {
        Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var subject = _context.ServiceProvider.GetRequiredService<DefaultAngleSharpV8Crawler>();
        var result = await subject.Start(FetchHostFixture.HostName, _context.CancellationSource.Token);

        AssertResult(result);
    }

    private void AssertResult(IScrapeResult result)
    {
        Assert.Equal(_context.Links.Count, result.Urls.Count);

        var firstNotSecond = _context.Links.Except(result.Urls).ToList();
        Assert.Empty(firstNotSecond);

        var secondNotFirst = result.Urls.Except(_context.Links).ToList();
        Assert.Empty(secondNotFirst);
    }
}
