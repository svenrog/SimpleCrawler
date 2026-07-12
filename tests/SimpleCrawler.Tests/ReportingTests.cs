using System.Diagnostics.CodeAnalysis;
using SimpleCrawler.Core;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.Tests;

/// <summary>
/// Guards per-URL reporting on IScrapeResult: every fetched page (success or failure) gets a UrlReport
/// carrying its status code, outcome and page data, while Urls stays the indexable-only subset.
/// </summary>
public class ReportingTests
{
    private const string _entry = "http://localhost/";

    [Fact]
    public async Task Reports_Cover_Successful_Pages_With_Status_And_Page_Data()
    {
        var crawler = CreateCrawler(new()
        {
            [_entry] = new(200, ["/a", "/b"]),
            ["http://localhost/a"] = new(200, []),
            ["http://localhost/b"] = new(200, []),
        });

        var result = await crawler.Start(_entry, TestContext.Current.CancellationToken);

        Assert.All(result.Reports, r => Assert.Equal(CrawlOutcome.Success, r.Outcome));

        var root = Assert.Single(result.Reports, r => r.Url == _entry);
        Assert.Equal(200, root.StatusCode);
        Assert.True(root.Indexed);
        Assert.True(root.Followed);
        Assert.Equal(2, root.LinkCount);
        Assert.NotNull(root.ParseDuration);
        Assert.Equal("text/html", root.ContentType);
        Assert.Equal(42, root.ContentLength);
    }

    [Fact]
    public async Task Failed_Fetches_Appear_In_Reports_But_Not_In_Urls()
    {
        var crawler = CreateCrawler(new()
        {
            [_entry] = new(200, ["/missing"]),
            ["http://localhost/missing"] = new(404, []),
        });

        var result = await crawler.Start(_entry, TestContext.Current.CancellationToken);

        var failure = Assert.Single(result.Reports, r => r.Url == "http://localhost/missing");
        Assert.Equal(CrawlOutcome.HttpError, failure.Outcome);
        Assert.Equal(404, failure.StatusCode);

        Assert.DoesNotContain("http://localhost/missing", result.Urls);
        Assert.Contains(_entry, result.Urls);
    }

    [Fact]
    public async Task Reports_Are_At_Least_As_Many_As_Urls()
    {
        var crawler = CreateCrawler(new()
        {
            [_entry] = new(200, ["/a", "/gone"]),
            ["http://localhost/a"] = new(200, []),
            ["http://localhost/gone"] = new(500, []),
        });

        var result = await crawler.Start(_entry, TestContext.Current.CancellationToken);

        Assert.True(result.Reports.Count >= result.Urls.Count);
        Assert.Equal(3, result.Reports.Count);
        Assert.Equal(2, result.Urls.Count);
    }

    private static ReportingCrawler CreateCrawler(Dictionary<string, PageStub> pages)
    {
        var options = new CrawlerOptions
        {
            CrawlDelay = 0,
            Concurrency = 4,
            RespectRobotsTxt = false,
            RespectMetaRobots = false,
            EnableSitemapDiscovery = false,
        };

        var client = new FakeRobotClient();
        return new ReportingCrawler(client, Options.Create(options), NullLogger.Instance, pages);
    }

    private readonly record struct PageStub(int Status, IReadOnlyList<string?> Links);

    private sealed class ReportingCrawler : AbstractRobotsCrawler<string, string, ScrapeResult>
    {
        private readonly IReadOnlyDictionary<string, PageStub> _pages;

        public ReportingCrawler(
            IRobotClient robotClient,
            IOptions<CrawlerOptions> options,
            Microsoft.Extensions.Logging.ILogger logger,
            IReadOnlyDictionary<string, PageStub> pages)
            : base(robotClient, options, logger)
        {
            _pages = pages;
        }

        protected override Task<string?> LoadResponse(string url, CancellationToken cancellationToken)
        {
            var status = _pages.TryGetValue(url, out var page) ? page.Status : 404;
            ReportResponse(url, new ResponseSignal { StatusCode = status, ContentLength = 42, ContentType = "text/html" });

            return Task.FromResult(status is >= 200 and <= 299 ? url : null);
        }

        protected override ValueTask<string> ParseResponse(string response)
            => new(response);

        protected override ValueTask<PageExtract> ExtractPageData(string document)
        {
            var links = _pages.TryGetValue(document, out var page) ? page.Links : [];
            return ValueTask.FromResult(new PageExtract(null, RobotsRules.All, links));
        }

        protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
            => ValueTask.FromResult(new ScrapeResult { Urls = [.. Visited], Reports = Reports });
    }

    private sealed class FakeRobotClient : IRobotClient
    {
        public Task<IRobotsTxt> LoadRobotsTxtAsync(Uri url, CancellationToken cancellationToken = default)
            => Task.FromResult<IRobotsTxt>(new FakeRobotsTxt());

        public IAsyncEnumerable<UrlSetItem> LoadSitemapsAsync(Uri uri, DateTime? modifiedSince = null, CancellationToken cancellationToken = default)
            => EmptySitemap();

        private static async IAsyncEnumerable<UrlSetItem> EmptySitemap()
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class FakeRobotsTxt : IRobotsTxt
    {
        public bool TryGetCrawlDelay(ProductToken userAgent, out int crawlDelay)
        {
            crawlDelay = 0;
            return false;
        }

        public bool TryGetRules(ProductToken userAgent, [NotNullWhen(true)] out IRobotRuleChecker? ruleChecker)
        {
            ruleChecker = null;
            return false;
        }

        public bool TryGetHost([NotNullWhen(true)] out string? host)
        {
            host = null;
            return false;
        }

        public IAsyncEnumerable<UrlSetItem> LoadSitemapAsync(Uri url, DateTime? modifiedSince = default, CancellationToken cancellationToken = default)
            => EmptySitemap();

        private static async IAsyncEnumerable<UrlSetItem> EmptySitemap()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
