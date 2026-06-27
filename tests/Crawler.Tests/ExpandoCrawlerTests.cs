using Crawler.AngleSharp.Js.Jint;
using Crawler.AngleSharp.Js.V8;
using Crawler.Tests.Fixtures;
using Crawler.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Crawler.Tests;

[Collection("Crawler")]
public class ExpandoCrawlerTests : IClassFixture<ExpandoHostFixture>
{
    private readonly ExpandoHostFixture _context;

    public ExpandoCrawlerTests(ExpandoHostFixture hostFixture)
    {
        _context = hostFixture;
    }

    [Fact]
    public async Task AngleSharpV8Crawler_Stores_DomExpandos()
    {
        Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var subject = _context.ServiceProvider.GetRequiredService<DefaultAngleSharpV8Crawler>();
        var result = await subject.Start(ExpandoHostFixture.HostName, _context.CancellationSource.Token);

        Assert.NotEmpty(_context.Links);
        Assert.Equal(_context.Links.Count, result.Urls.Count);
        Assert.Equal(_context.Links.ToHashSet(), [.. result.Urls]);
    }

    [Fact]
    public async Task AngleSharpJintCrawler_Stores_DomExpandos()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultAngleSharpJintCrawler>();
        var result = await subject.Start(ExpandoHostFixture.HostName, _context.CancellationSource.Token);

        Assert.NotEmpty(_context.Links);
        Assert.Equal(_context.Links.Count, result.Urls.Count);
        Assert.Equal(_context.Links.ToHashSet(), [.. result.Urls]);
    }
}
