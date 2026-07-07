using SimpleCrawler.Core;
using SimpleCrawler.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
        Assert.True(await crawler.FetchStarted.WaitAsync(TimeSpan.FromSeconds(5)));

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    // Mimics a headless backend: LoadResponse blocks on a call that is only cancelable because it is
    // wrapped in WaitAsync(token) - exactly the pattern the Playwright/Puppeteer crawlers use.
    private sealed class HangingCrawler : AbstractCrawler<string, string, ScrapeResult>
    {
        public readonly SemaphoreSlim FetchStarted = new(0, 1);

        public HangingCrawler(IOptions<CrawlerOptions> options) : base(options, NullLogger.Instance)
        {
        }

        protected override async Task<string?> LoadResponse(string url, CancellationToken cancellationToken)
        {
            FetchStarted.Release();
            await Task.Delay(Timeout.Infinite).WaitAsync(cancellationToken);
            return null;
        }

        protected override ValueTask<string> ParseResponse(string response) => new(response);

        protected override ValueTask<PageExtract> ExtractPageData(string document) =>
            new(new PageExtract(null, RobotsRules.All, []));

        protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken) =>
            new(new ScrapeResult { Urls = Visited });
    }
}
