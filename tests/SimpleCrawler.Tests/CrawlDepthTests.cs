using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SimpleCrawler.Core;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Robots;
using SimpleCrawler.Tests.Helpers;
using System.Collections.Concurrent;

namespace SimpleCrawler.Tests;

/// <summary>
/// Covers the max-depth limit: entries are depth 0, each followed link is one deeper, and the reported
/// depth reflects the path taken. A limit of 0 imposes no bound.
/// </summary>
public class CrawlDepthTests
{
    private const string _entry = "http://localhost/";
    private const string _a = "http://localhost/a";
    private const string _b = "http://localhost/b";
    private const string _c = "http://localhost/c";

    private static readonly Dictionary<string, IReadOnlyList<string?>> _chain = new()
    {
        [_entry] = [_a],
        [_a] = [_b],
        [_b] = [_c],
    };

    [Fact]
    public async Task MaxDepth_One_Stops_After_One_Hop()
    {
        var crawler = CreateCrawler(maxDepth: 1);
        await crawler.Start(_entry, TestContext.Current.CancellationToken);

        Assert.Equal([_entry, _a], crawler.Fetched.OrderBy(u => u));
    }

    [Fact]
    public async Task MaxDepth_Two_Reaches_Two_Hops()
    {
        var crawler = CreateCrawler(maxDepth: 2);
        await crawler.Start(_entry, TestContext.Current.CancellationToken);

        Assert.Equal([_entry, _a, _b], crawler.Fetched.OrderBy(u => u));
    }

    [Fact]
    public async Task MaxDepth_Zero_Is_Unbounded()
    {
        var crawler = CreateCrawler(maxDepth: 0);
        await crawler.Start(_entry, TestContext.Current.CancellationToken);

        Assert.Equal([_entry, _a, _b, _c], crawler.Fetched.OrderBy(u => u));
    }

    [Fact]
    public async Task Reports_Carry_Path_Depth()
    {
        var crawler = CreateCrawler(maxDepth: 0);
        var result = await crawler.Start(_entry, TestContext.Current.CancellationToken);

        var byUrl = result.Reports.ToDictionary(r => r.Url, r => r.Depth);

        Assert.Equal(0, byUrl[_entry]);
        Assert.Equal(1, byUrl[_a]);
        Assert.Equal(2, byUrl[_b]);
        Assert.Equal(3, byUrl[_c]);
    }

    private static RecordingCrawler CreateCrawler(int maxDepth)
    {
        var options = new CrawlerOptions
        {
            CrawlDelay = 0,
            Concurrency = 2,
            MaxDepth = maxDepth,
            RespectRobotsTxt = false,
            RespectMetaRobots = false,
            EnableSitemapDiscovery = false,
        };

        return new RecordingCrawler(new StubRobotClient(), Options.Create(options), NullLogger.Instance);
    }

    private sealed class RecordingCrawler : AbstractRobotsCrawler<string, string, ScrapeResult>
    {
        public RecordingCrawler(IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger logger)
            : base(robotClient, options, logger)
        {
        }

        public ConcurrentBag<string> Fetched { get; } = [];

        protected override Task<string?> LoadResponse(string url, CancellationToken cancellationToken)
        {
            Fetched.Add(url);
            return Task.FromResult<string?>(url);
        }

        protected override ValueTask<string> ParseResponse(string response)
            => new(response);

        protected override ValueTask<PageExtract> ExtractPageData(string document)
        {
            var hrefs = _chain.TryGetValue(document, out var links) ? links : [];
            return new ValueTask<PageExtract>(new PageExtract(null, RobotsRules.All, hrefs));
        }

        protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
            => new(new ScrapeResult { Urls = [.. Visited], Reports = Reports });
    }
}
