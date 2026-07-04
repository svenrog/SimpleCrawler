using BenchmarkDotNet.Attributes;
using Crawler.Core.Helpers;
using Crawler.Core.Robots;

namespace Crawler.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class RobotsBenchmarks
{
    private static readonly List<string> _paths =
    [
        "/some/path",
        "/some/path/%E3%83%84",
        "/some/path",
        "/foo/bar?baz=https://foo.bar",
        "/foo/bar/%E3%83%84",
        "/some/path",
        "/some/path/%E3%83%84",
        "/some/path",
        "/some/path/%E3%83%84",
        "/some/path",
        "/some/path/%E3%83%84",
    ];

    private static readonly List<string> _patterns =
    [
        "/some/path%3c",
        "/some/path%3C",
        "/some*path%2F",
        "/some*path/some*path/some*path",
        "/some%24path",
        "/some$path",
        "/some/path~*path*path",
        "/foo/bar?baz=https://foo.bar",
        "/foo/bar*?baz=https%3A%2F%2Ffoo.bar",
        "/foo/bar/%E3%83%84",
        "/foo/bar/ツ"
    ];

    private static readonly List<UrlPathPattern> _urlPatterns = [.. _patterns.Select(x => new UrlPathPattern(x))];
    private static readonly List<UriPath> _uriPaths = [.. _paths.Select(x => new UriPath(x))];

    private static readonly string[] _rulePatterns =
    [
        "/admin", "/private/*", "/api/*/internal", "/search", "/cart",
        "/checkout", "/user/*/settings", "/*.json$", "/tmp/", "/login",
        "/assets/*", "/draft/*", "/*?sessionid=", "/legacy/", "/beta/*",
    ];

    private static readonly string[] _checkPaths =
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

    private static readonly string[] _metaRobots =
    [
        "index, follow",
        "noindex, follow",
        "noindex,nofollow",
        "all",
        "INDEX, FOLLOW",
    ];

    private RobotRuleChecker _checker = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rules = new HashSet<UrlRule>();
        for (var i = 0; i < _rulePatterns.Length; i++)
        {
            var type = i % 4 == 0 ? RuleType.Allow : RuleType.Disallow;
            rules.Add(new UrlRule(type, new UrlPathPattern(_rulePatterns[i])));
        }

        _checker = new RobotRuleChecker(rules);
    }

    [Benchmark]
    public void UrlPathPattern_Matches()
    {
        for (var i = 0; i < _patterns.Count; i++)
        {
            var pattern = _urlPatterns[i];
            var path = _uriPaths[i];

            pattern.Matches(path);
        }
    }

    [Benchmark]
    public int IsAllowed()
    {
        var allowed = 0;

        foreach (var path in _checkPaths)
        {
            if (_checker.IsAllowed(path))
                allowed++;
        }

        return allowed;
    }

    [Benchmark]
    public int ParseMetaRobots()
    {
        var indexable = 0;

        foreach (var input in _metaRobots)
        {
            var rules = IndexingHelper.ParseMetaRobots(input);
            if (rules.Index) indexable++;
            if (rules.Follow) indexable++;
        }

        return indexable;
    }
}
