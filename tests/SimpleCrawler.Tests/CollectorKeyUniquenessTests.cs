using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SimpleCrawler.Core;
using SimpleCrawler.Core.Collectors;
using SimpleCrawler.Core.Models;

namespace SimpleCrawler.Tests;

/// <summary>
/// Guards the <see cref="AbstractCrawler{TResponse, TDocument, TResult}"/> constructor check that no two
/// registered <see cref="IDomCollector"/>s share a <see cref="IDomCollector.Key"/>: on the rendered backends a
/// collision corrupts the in-page envelope silently, so the failure must surface at construction instead.
/// </summary>
public class CollectorKeyUniquenessTests
{
    [Fact]
    public void Duplicate_Keys_Throw_At_Construction()
    {
        var collectors = new ICrawlCollector[] { new KeyedCollector("dup"), new KeyedCollector("dup") };

        var thrown = Assert.Throws<InvalidOperationException>(() => new StubCrawler(collectors));
        Assert.Contains("dup", thrown.Message);
    }

    [Fact]
    public void Distinct_Keys_Construct_Without_Throwing()
    {
        var collectors = new ICrawlCollector[] { new KeyedCollector("a"), new KeyedCollector("b") };

        // No throw — distinct keys are the supported case.
        _ = new StubCrawler(collectors);
    }

    private sealed class StubCrawler : AbstractCrawler<string, string, ScrapeResult>
    {
        public StubCrawler(IEnumerable<ICrawlCollector> collectors)
            : base(Options.Create(new CrawlerOptions()), NullLogger.Instance, null, collectors)
        {
        }

        // The guard runs in the constructor, so these are never reached for these tests.
        protected override Task<string?> LoadResponse(string url, CancellationToken cancellationToken) => throw new NotImplementedException();
        protected override ValueTask<string> ParseResponse(string response) => throw new NotImplementedException();
        protected override ValueTask<PageExtract> ExtractPageData(string document) => throw new NotImplementedException();
        protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class KeyedCollector : IDomCollector
    {
        private readonly string _key;

        public KeyedCollector(string key)
        {
            _key = key;
        }

        public string Key => _key;

        public void OnResponse(UrlReport report, ResponseSignal response)
        {
        }
    }
}
