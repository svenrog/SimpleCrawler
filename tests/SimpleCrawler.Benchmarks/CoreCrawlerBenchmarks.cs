using BenchmarkDotNet.Attributes;
using SimpleCrawler.Benchmarks.Crawlers;
using SimpleCrawler.Core;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class CoreCrawlerBenchmarks
{
    private const int _pageCount = 20000;

    private SyntheticCrawler _crawler = null!;

    [Params(8, 32)]
    public int Concurrency;

    [GlobalSetup]
    public void Setup()
    {
        var options = new CrawlerOptions
        {
            CrawlDelay = 0,
            Concurrency = Concurrency,
            MaxPages = int.MaxValue,
            RespectMetaRobots = true,
        };

        _crawler = new SyntheticCrawler(_pageCount, fanout: 8, "index, follow", Options.Create(options));
    }

    [Benchmark]
    public async Task Crawl()
    {
        await _crawler.Start(SyntheticCrawler.Entry, CancellationToken.None);
    }
}
