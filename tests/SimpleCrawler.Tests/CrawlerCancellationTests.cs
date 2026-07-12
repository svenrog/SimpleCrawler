using SimpleCrawler.Core;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Core.Robots;
using SimpleCrawler.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace SimpleCrawler.Tests;

public class CrawlerCancellationTests
{
    [Fact]
    public async Task Start_Surfaces_Cancellation_While_A_Fetch_Is_In_Flight()
    {
        var options = Options.Create(new CrawlerOptions
        {
            CrawlDelay = 0,
            Concurrency = 2,
            RespectRobotsTxt = false,
            RespectMetaRobots = false,
            EnableSitemapDiscovery = false,
        });

        var crawler = new HangingCrawler(options);
        using var cts = new CancellationTokenSource();

        var run = crawler.Start(["https://example.com/"], cts.Token);
        Assert.True(await crawler.FetchStarted.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    /// <summary>
    /// Mimics a headless backend: LoadResponse blocks on a call that is only cancelable because it is
    /// wrapped in WaitAsync(token) - exactly the pattern the Playwright/Puppeteer crawlers use.
    /// </summary>
    private sealed class HangingCrawler : AbstractCrawler<string, string, ScrapeResult>
    {
        public readonly SemaphoreSlim FetchStarted = new(0, 1);

        public HangingCrawler(IOptions<CrawlerOptions> options) : base(options, NullLogger.Instance)
        {
        }

        protected override async Task<string?> LoadResponse(string url, CancellationToken cancellationToken)
        {
            FetchStarted.Release();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return null;
        }

        protected override ValueTask<string> ParseResponse(string response) => new(response);

        protected override ValueTask<PageExtract> ExtractPageData(string document) =>
            new(new PageExtract(null, RobotsRules.All, []));

        protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken) =>
            new(new ScrapeResult { Urls = Visited });
    }

    [Fact]
    public async Task Start_Surfaces_Cancellation_While_A_Browser_Launch_Is_Wedged()
    {
        var options = Options.Create(new HeadlessCrawlerOptions(new CrawlerOptions
        {
            CrawlDelay = 0,
            Concurrency = 2,
            RespectRobotsTxt = false,
            RespectMetaRobots = false,
            EnableSitemapDiscovery = false,
        }));

        var crawler = new WedgedLaunchCrawler(new StubRobotClient(), options);
        using var cts = new CancellationTokenSource();

        var run = crawler.Start(["https://example.com/"], cts.Token);
        Assert.True(await crawler.AcquireStarted.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    /// <summary>
    /// Models a headless backend whose first-use browser launch (inside page acquisition) honors no token
    /// and never completes - the deadlock that hangs a crawl worker before it ever reaches the cancelable
    /// navigation. Guards that AttemptLoad now abandons a stuck acquisition on cancel.
    /// </summary>
    private sealed class WedgedLaunchCrawler : AbstractHeadlessCrawler<object, ScrapeResult>
    {
        public readonly SemaphoreSlim AcquireStarted = new(0, 2);

        public WedgedLaunchCrawler(IRobotClient robotClient, IOptions<HeadlessCrawlerOptions> options)
            : base(robotClient, options, NullLogger.Instance)
        {
        }

        protected override async Task<object> NewPageAsync(ProxyInfo? proxy)
        {
            AcquireStarted.Release();
            await Task.Delay(Timeout.Infinite, CancellationToken.None);
            return new object();
        }

        protected override Task<(int? Status, IReadOnlyDictionary<string, string>? Headers)> NavigateAsync(object page, string url, ProxyInfo? proxy, CancellationToken cancellationToken) =>
            Task.FromResult<(int? Status, IReadOnlyDictionary<string, string>? Headers)>((200, null));

        protected override Task ClosePageCore(object page) => Task.CompletedTask;

        protected override Task<JsonElement> EvaluateExtractorAsync(object page, string script, CancellationToken cancellationToken) =>
            Task.FromResult(default(JsonElement));

        protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken) =>
            new(new ScrapeResult { Urls = Visited });
    }
}
