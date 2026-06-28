using Crawler.AngleSharp.Js.Jint;
using Crawler.AngleSharp.Js.V8;
using Crawler.Tests.Fixtures;
using Crawler.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Crawler.Tests;

// Regression for the HTMLAnchorElement href setter: assigning `anchor.href = url` on a freshly created
// anchor must not throw. The setter used to resolve the value against the element's own (empty) href via
// `new Uri("")`, throwing "Invalid URI: The URI is empty.", which surfaced through Jint's CatchClrExceptions
// into the bundle and tripped its error boundary so no links rendered (the prep.öob.se symptom).
[Collection("Crawler")]
public class AnchorHrefCrawlerTests : IClassFixture<AnchorHrefHostFixture>
{
    private readonly AnchorHrefHostFixture _context;

    public AnchorHrefCrawlerTests(AnchorHrefHostFixture hostFixture)
    {
        _context = hostFixture;
    }

    [Fact]
    public async Task AngleSharpJintCrawler_Resolves_Anchor_Href()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultAngleSharpJintCrawler>();
        var result = await subject.Start(AnchorHrefHostFixture.HostName, _context.CancellationSource.Token);

        Assert.NotEmpty(_context.Links);
        Assert.Equal(_context.Links.Count, result.Urls.Count);
        Assert.Equal(_context.Links.ToHashSet(), [.. result.Urls]);
    }

    [Fact]
    public async Task AngleSharpV8Crawler_Resolves_Anchor_Href()
    {
        Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var subject = _context.ServiceProvider.GetRequiredService<DefaultAngleSharpV8Crawler>();
        var result = await subject.Start(AnchorHrefHostFixture.HostName, _context.CancellationSource.Token);

        Assert.NotEmpty(_context.Links);
        Assert.Equal(_context.Links.Count, result.Urls.Count);
        Assert.Equal(_context.Links.ToHashSet(), [.. result.Urls]);
    }
}
