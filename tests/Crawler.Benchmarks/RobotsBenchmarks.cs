using BenchmarkDotNet.Attributes;
using Crawler.Core.Robots;
using System.Collections.Generic;
using System.Linq;

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

    private static readonly List<UrlPathPattern> _urlPatterns = [.. _paths.Select(x => new UrlPathPattern(x))];
    private static readonly List<UriPath> _uriPaths = [.. _paths.Select(x => new UriPath(x))];

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
}
