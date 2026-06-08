using BenchmarkDotNet.Attributes;
using Crawler.Core;
using Crawler.Core.Helpers;
using Crawler.Core.Robots;
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
            Concurrency = Parallelism,
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

[MemoryDiagnoser]
[ShortRunJob]
public class RobotRuleCheckerBenchmarks
{
    private static readonly string[] _patterns =
    [
        "/admin", "/private/*", "/api/*/internal", "/search", "/cart",
        "/checkout", "/user/*/settings", "/*.json$", "/tmp/", "/login",
        "/assets/*", "/draft/*", "/*?sessionid=", "/legacy/", "/beta/*",
    ];

    private static readonly string[] _paths =
    [
        "/products/widget-123",
        "/api/v2/internal/stats",
        "/user/42/settings",
        "/blog/2026/perf-notes",
        "/assets/app.css",
        "/data/export.json",
        "/about/team",
        "/checkout/step-2",
    ];

    private RobotRuleChecker _checker;

    [GlobalSetup]
    public void Setup()
    {
        var rules = new HashSet<UrlRule>();
        for (var i = 0; i < _patterns.Length; i++)
        {
            var type = i % 4 == 0 ? RuleType.Allow : RuleType.Disallow;
            rules.Add(new UrlRule(type, new UrlPathPattern(_patterns[i])));
        }

        _checker = new RobotRuleChecker(rules);
    }

    [Benchmark]
    public int IsAllowed()
    {
        var allowed = 0;

        foreach (var path in _paths)
        {
            if (_checker.IsAllowed(path))
                allowed++;
        }

        return allowed;
    }
}
