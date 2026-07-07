using SimpleCrawler.Tests.Assertions;
using SimpleCrawler.Tests.Fixtures;

namespace SimpleCrawler.Tests;

[Collection("Crawler")]
public class MultiHostCrawlerTests : IClassFixture<MultiHostFixture>
{
    private readonly MultiHostFixture _context;

    public MultiHostCrawlerTests(MultiHostFixture context)
    {
        _context = context;
    }

    [Fact]
    public async Task Crawls_Union_Of_Listed_Hosts_And_Excludes_Others()
    {
        var subject = _context.CreateCrawler();

        var result = await subject.Start(
            [MultiHostFixture.HostA, MultiHostFixture.HostB],
            TestContext.Current.CancellationToken);

        string[] expected =
        [
            $"{MultiHostFixture.HostA}",
            $"{MultiHostFixture.HostA}a-1",
            $"{MultiHostFixture.HostB}",
            $"{MultiHostFixture.HostB}b-1",
        ];

        LinkAssertions.AssertSameLinks(expected, result.Urls);
    }

    [Fact]
    public async Task Single_Entry_Excludes_The_Unlisted_Host()
    {
        var subject = _context.CreateCrawler();

        var result = await subject.Start(MultiHostFixture.HostA, TestContext.Current.CancellationToken);

        string[] expected =
        [
            $"{MultiHostFixture.HostA}",
            $"{MultiHostFixture.HostA}a-1",
        ];

        LinkAssertions.AssertSameLinks(expected, result.Urls);
    }
}
