using BenchmarkDotNet.Attributes;
using Crawler.Core;
using Crawler.Core.Helpers;
using Microsoft.Extensions.Options;

namespace Crawler.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class CoreCrawlerBenchmarks
{
    private const int _pageCount = 20000;

    private SyntheticCrawler _crawler;

    [Params(8, 32)]
    public int Parallelism;

    [GlobalSetup]
    public void Setup()
    {
        var options = new CrawlerOptions
        {
            CrawlDelay = 0,
            Parallelism = Parallelism,
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

[MemoryDiagnoser]
[ShortRunJob]
public class MetaRobotsBenchmarks
{
    private readonly string[] _inputs =
    [
        "index, follow",
        "noindex, follow",
        "noindex,nofollow",
        "all",
        "INDEX, FOLLOW",
    ];

    [Benchmark]
    public int ParseMetaRobots()
    {
        var indexable = 0;

        foreach (var input in _inputs)
        {
            var rules = IndexingHelper.ParseMetaRobots(input);
            if (rules.Index) indexable++;
            if (rules.Follow) indexable++;
        }

        return indexable;
    }
}
