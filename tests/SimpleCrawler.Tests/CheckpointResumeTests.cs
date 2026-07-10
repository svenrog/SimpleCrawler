using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SimpleCrawler.Core;
using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Robots;
using SimpleCrawler.Tests.Helpers;
using System.Collections.Concurrent;

namespace SimpleCrawler.Tests;

/// <summary>
/// Guards checkpoint resume: a matching checkpoint seeds the crawl so already-processed URLs are not
/// re-fetched and only the pending frontier is drained; a fresh crawl writes its progress; a checkpoint
/// taken for different entry points is ignored.
/// </summary>
public class CheckpointResumeTests
{
    private const string _entry = "http://localhost/";
    private const string _urlA = "http://localhost/a";
    private const string _urlB = "http://localhost/b";

    [Fact]
    public async Task Resume_Skips_Processed_And_Drains_Pending()
    {
        var store = new MemoryCheckpointStore
        {
            ToLoad = new CrawlState
            {
                Entries = [_entry],
                Discovered = [_entry, _urlA, _urlB],
                Processed = [_entry, _urlA],
                Visited = [_entry, _urlA],
            },
        };

        var crawler = CreateCrawler(store, links: []);
        var result = await crawler.Start(_entry, TestContext.Current.CancellationToken);

        Assert.Equal([_urlB], crawler.Fetched);

        Assert.Contains(_entry, result.Urls);
        Assert.Contains(_urlA, result.Urls);
        Assert.Contains(_urlB, result.Urls);
    }

    [Fact]
    public async Task Resume_Preserves_Reports_From_Before_Checkpoint()
    {
        var priorReport = new UrlReport { Url = _urlA, StatusCode = 200, Outcome = CrawlOutcome.Success };
        var store = new MemoryCheckpointStore
        {
            ToLoad = new CrawlState
            {
                Entries = [_entry],
                Discovered = [_entry, _urlA, _urlB],
                Processed = [_entry, _urlA],
                Visited = [_entry, _urlA],
                Reports = new ConcurrentDictionary<string, UrlReport>(
                [
                    new(_entry, new UrlReport { Url = _entry, StatusCode = 200, Outcome = CrawlOutcome.Success }),
                    new(_urlA, priorReport),
                ]),
            },
        };

        var crawler = CreateCrawler(store, links: []);
        var result = await crawler.Start(_entry, TestContext.Current.CancellationToken);

        // Only the pending frontier is fetched this session, yet the report covers every page.
        Assert.Equal([_urlB], crawler.Fetched);
        Assert.Equal(3, result.Reports.Count);

        // The pre-checkpoint report is carried through untouched, not re-created from a partial re-fetch.
        Assert.Same(priorReport, Assert.Single(result.Reports, r => r.Url == _urlA));
        Assert.Contains(result.Reports, r => r.Url == _entry);
        Assert.Contains(result.Reports, r => r.Url == _urlB);
    }

    [Fact]
    public async Task Fresh_Crawl_Saves_Progress()
    {
        var store = new MemoryCheckpointStore();

        var crawler = CreateCrawler(store, links: new()
        {
            [_entry] = [_urlA],
        });

        await crawler.Start(_entry, TestContext.Current.CancellationToken);

        Assert.NotNull(store.Saved);
        Assert.Contains(_entry, (IEnumerable<string>)store.Saved!.Processed);
        Assert.Contains(_urlA, (IEnumerable<string>)store.Saved.Processed);
        Assert.Contains(_entry, (IEnumerable<string>)store.Saved.Visited);
        Assert.Contains(_urlA, (IEnumerable<string>)store.Saved.Visited);
    }

    [Fact]
    public async Task Start_Persists_Checkpoint_When_Cancelled()
    {
        var store = new MemoryCheckpointStore();
        var crawler = CreateCrawler(store, links: [], block: true);

        using var cts = new CancellationTokenSource();
        var run = crawler.Start(_entry, cts.Token);

        await crawler.FetchStarted.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        Assert.NotNull(store.Saved);
    }

    [Fact]
    public async Task Checkpoint_For_Different_Entries_Is_Ignored()
    {
        var store = new MemoryCheckpointStore
        {
            ToLoad = new CrawlState
            {
                Entries = ["http://other/"],
                Discovered = ["http://other/", "http://other/x"],
                Processed = ["http://other/"],
                Visited = ["http://other/"],
            },
        };

        var crawler = CreateCrawler(store, links: []);
        await crawler.Start(_entry, TestContext.Current.CancellationToken);

        Assert.Contains(_entry, crawler.Fetched);
    }

    private static RecordingCrawler CreateCrawler(ICheckpointStore store, Dictionary<string, IReadOnlyList<string?>> links, bool block = false)
    {
        var options = new CrawlerOptions
        {
            CrawlDelay = 0,
            Concurrency = 2,
            RespectRobotsTxt = false,
            RespectMetaRobots = false,
            EnableSitemapDiscovery = false,
        };

        return new RecordingCrawler(new StubRobotClient(), Options.Create(options), NullLogger.Instance, links, store, block);
    }

    private sealed class RecordingCrawler : AbstractRobotsCrawler<string, string, ScrapeResult>
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<string?>> _links;
        private readonly bool _block;

        public RecordingCrawler(
            IRobotClient robotClient,
            IOptions<CrawlerOptions> options,
            ILogger logger,
            IReadOnlyDictionary<string, IReadOnlyList<string?>> links,
            ICheckpointStore checkpoint,
            bool block = false)
            : base(robotClient, options, logger, checkpoint)
        {
            _links = links;
            _block = block;
        }

        public ConcurrentBag<string> Fetched { get; } = [];
        private readonly TaskCompletionSource _firstFetch = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task FetchStarted => _firstFetch.Task;

        protected override async Task<string?> LoadResponse(string url, CancellationToken cancellationToken)
        {
            Fetched.Add(url);
            _firstFetch.TrySetResult();

            if (_block)
                await Task.Delay(Timeout.Infinite, cancellationToken);

            return url;
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

    private sealed class MemoryCheckpointStore : ICheckpointStore
    {
        public CrawlState? ToLoad { get; set; }
        public CrawlState? Saved { get; private set; }

        public string Target => "memory";

        public ValueTask<CrawlState?> LoadAsync(CancellationToken cancellationToken)
            => new(ToLoad);

        public ValueTask SaveAsync(CrawlState state, CancellationToken cancellationToken)
        {
            Saved = state;
            return ValueTask.CompletedTask;
        }
    }
}
