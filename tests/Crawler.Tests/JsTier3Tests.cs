using Crawler.AngleSharp.Js.Jint;
using Crawler.AngleSharp.Js.V8;
using Crawler.Core.Models;
using Crawler.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Crawler.Tests;

[Collection("Crawler")]
public class JsTier3Tests : IClassFixture<JsTier3HostFixture>
{
    private readonly JsTier3HostFixture _context;

    public JsTier3Tests(JsTier3HostFixture hostFixture)
    {
        _context = hostFixture;
    }

    [Fact]
    public async Task JintCrawler_Renders_Preact_And_Crawls()
    {
        var subject = _context.ServiceProvider.GetRequiredService<JintAngleSharpJsCrawler>();
        var result = await subject.Start(JsTier3HostFixture.HostName, _context.CancellationSource.Token);

        AssertResult(result);
    }

    [Fact]
    public async Task V8Crawler_Renders_Preact_And_Crawls()
    {
        Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var subject = _context.ServiceProvider.GetRequiredService<V8AngleSharpJsCrawler>();
        var result = await subject.Start(JsTier3HostFixture.HostName, _context.CancellationSource.Token);

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
