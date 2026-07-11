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
/// Drives include/exclude filtering through the real discovery pipeline: patterns gate discovered links,
/// while explicitly-provided entry points are always crawled.
/// </summary>
public class UrlFilterCrawlTests
{
    private const string _entry = "http://localhost/";
    private const string _post = "http://localhost/blog/post";
    private const string _private = "http://localhost/blog/private";
    private const string _about = "http://localhost/about";

    private static readonly Dictionary<string, IReadOnlyList<string?>> _links = new()
    {
        [_entry] = [_post, _private, _about],
    };

    [Fact]
    public async Task Exclude_Drops_Matching_Discovered_Links()
    {
        var crawler = CreateCrawler(includes: [], excludes: ["/blog/private"]);
        await crawler.Start(_entry, TestContext.Current.CancellationToken);

        Assert.Equal([_entry, _about, _post], crawler.Fetched.OrderBy(u => u));
    }

    [Fact]
    public async Task Include_Keeps_Only_Matching_Discovered_Links_But_Still_Crawls_Entry()
    {
        var crawler = CreateCrawler(includes: ["/blog/*"], excludes: []);
        await crawler.Start(_entry, TestContext.Current.CancellationToken);

        // The entry's own path does not match the include, yet it is crawled because entries are exempt.
        Assert.Equal([_entry, _post, _private], crawler.Fetched.OrderBy(u => u));
    }

    private static RecordingCrawler CreateCrawler(IReadOnlyList<string> includes, IReadOnlyList<string> excludes)
    {
        var options = new CrawlerOptions
        {
            CrawlDelay = 0,
            Concurrency = 2,
            IncludePatterns = includes,
            ExcludePatterns = excludes,
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
            var hrefs = _links.TryGetValue(document, out var links) ? links : [];
            return new ValueTask<PageExtract>(new PageExtract(null, RobotsRules.All, hrefs));
        }

        protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
            => new(new ScrapeResult { Urls = [.. Visited], Reports = Reports });
    }
}
