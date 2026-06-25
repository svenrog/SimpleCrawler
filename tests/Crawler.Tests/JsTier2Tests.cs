using Crawler.AngleSharp.Js.Jint;
using Crawler.AngleSharp.Js.V8;
using Crawler.Core.Models;
using Crawler.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Crawler.Tests;

[Collection("Crawler")]
public class JsTier2Tests : IClassFixture<JsTier2HostFixture>
{
    private readonly JsTier2HostFixture _context;

    public JsTier2Tests(JsTier2HostFixture hostFixture)
    {
        _context = hostFixture;
    }

    [Fact]
    public async Task JintCrawler_Fetches_Bundle_And_Crawls()
    {
        var subject = _context.ServiceProvider.GetRequiredService<JintAngleSharpJsCrawler>();
        var result = await subject.Start(JsTier2HostFixture.HostName, _context.CancellationSource.Token);

        AssertResult(result);
    }

    [Fact]
    public async Task V8Crawler_Fetches_Bundle_And_Crawls()
    {
        Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var subject = _context.ServiceProvider.GetRequiredService<V8AngleSharpJsCrawler>();
        var result = await subject.Start(JsTier2HostFixture.HostName, _context.CancellationSource.Token);

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
