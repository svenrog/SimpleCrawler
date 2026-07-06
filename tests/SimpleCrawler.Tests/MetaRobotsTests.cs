using Crawler.HtmlAgilityPack;
using Crawler.Tests.Assertions;
using Crawler.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Crawler.Tests;

[Collection("Crawler")]
public class MetaRobotsTests : IClassFixture<MetaRobotsHostFixture>
{
    private readonly MetaRobotsHostFixture _context;

    public MetaRobotsTests(MetaRobotsHostFixture hostFixture)
    {
        _context = hostFixture;
    }

    [Fact]
    public async Task Crawler_Excludes_Noindex_Pages_When_Respecting_Meta_Robots()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultHtmlAgilityPackCrawler>();
        var result = await subject.Start(MetaRobotsHostFixture.HostName, _context.CancellationSource.Token);

        Assert.DoesNotContain(MetaRobotsHostFixture.HiddenUrl, result.Urls);

        LinkAssertions.AssertSameLinks(_context.Links, result.Urls);
    }
}
