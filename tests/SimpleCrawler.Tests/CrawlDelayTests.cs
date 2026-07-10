using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using SimpleCrawler.Core;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.Tests;

/// <summary>
/// Guards the crawl-delay throttle: robots.txt Crawl-delay must actually slow requests (it was parsed but
/// ignored because the delay was snapshotted in the constructor), and it may only raise the configured
/// floor, never lower it.
/// </summary>
public class CrawlDelayTests
{
    private const string _entry = "http://localhost/";

    [Fact]
    public async Task Crawl_Honours_Robots_Crawl_Delay()
    {
        var options = CreateOptions(configuredDelay: 0);
        var crawler = CreateCrawler(options, robotsDelay: 1, links: new()
        {
            [_entry] = ["/a"],
        });

        var stopwatch = Stopwatch.StartNew();
        await crawler.Start(_entry, TestContext.Current.CancellationToken);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(900),
            $"Expected the 1s robots.txt Crawl-delay to space the two fetches; elapsed {stopwatch.ElapsedMilliseconds}ms.");
    }

    [Theory]
    [InlineData(0.0, 3, 3.0)]
    [InlineData(5.0, 2, 5.0)]
    [InlineData(2.0, null, 2.0)]
    public async Task Effective_Delay_Is_Max_Of_Configured_And_Robots(double configured, int? robotsDelay, double expected)
    {
        var options = CreateOptions(configuredDelay: configured);
        var crawler = CreateCrawler(options, robotsDelay, links: []);

        await crawler.Start(_entry, TestContext.Current.CancellationToken);

        Assert.Equal(expected, crawler.EffectiveDelay(_entry));
    }

    [Fact]
    public async Task Robots_Crawl_Delay_Is_Ignored_When_Not_Respecting_Robots()
    {
        var options = CreateOptions(configuredDelay: 0);
        options.RespectRobotsTxt = false;
        var crawler = CreateCrawler(options, robotsDelay: 10, links: []);

        await crawler.Start(_entry, TestContext.Current.CancellationToken);

        Assert.Equal(0, crawler.EffectiveDelay(_entry));
    }

    private static CrawlerOptions CreateOptions(double configuredDelay) => new()
    {
        CrawlDelay = configuredDelay,
        Concurrency = 4,
        RespectRobotsTxt = true,
        RespectMetaRobots = false,
        EnableSitemapDiscovery = false,
    };

    private static InMemoryRobotsCrawler CreateCrawler(
        CrawlerOptions options, int? robotsDelay, Dictionary<string, IReadOnlyList<string?>> links)
    {
        var robots = new FakeRobotsTxt(robotsDelay);
        var client = new FakeRobotClient(robots);
        return new InMemoryRobotsCrawler(client, Options.Create(options), NullLogger.Instance, links);
    }

    private sealed class InMemoryRobotsCrawler : AbstractRobotsCrawler<string, string, ScrapeResult>
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<string?>> _links;

        public InMemoryRobotsCrawler(
            IRobotClient robotClient,
            IOptions<CrawlerOptions> options,
            Microsoft.Extensions.Logging.ILogger logger,
            IReadOnlyDictionary<string, IReadOnlyList<string?>> links)
            : base(robotClient, options, logger)
        {
            _links = links;
        }

        protected override Task<string?> LoadResponse(string url, CancellationToken cancellationToken)
            => Task.FromResult<string?>(url);

        protected override ValueTask<string> ParseResponse(string response)
            => new(response);

        protected override ValueTask<PageExtract> ExtractPageData(string document)
        {
            var hrefs = _links.TryGetValue(document, out var links) ? links : [];
            return ValueTask.FromResult(new PageExtract(null, RobotsRules.All, hrefs));
        }

        protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
            => ValueTask.FromResult(new ScrapeResult { Urls = [.. Visited] });

        public double EffectiveDelay(string url) => GetCrawlDelay(new Uri(url).Authority);
    }

    private sealed class FakeRobotClient : IRobotClient
    {
        private readonly IRobotsTxt _robots;

        public FakeRobotClient(IRobotsTxt robots) => _robots = robots;

        public Task<IRobotsTxt> LoadRobotsTxtAsync(Uri url, CancellationToken cancellationToken = default)
            => Task.FromResult(_robots);

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
        private readonly int? _crawlDelay;

        public FakeRobotsTxt(int? crawlDelay) => _crawlDelay = crawlDelay;

        public bool TryGetCrawlDelay(ProductToken userAgent, out int crawlDelay)
        {
            crawlDelay = _crawlDelay ?? 0;
            return _crawlDelay.HasValue;
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
