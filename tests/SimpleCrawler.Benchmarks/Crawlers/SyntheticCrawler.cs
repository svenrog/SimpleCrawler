using SimpleCrawler.Core;
using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.Benchmarks;

public sealed class SyntheticCrawler : AbstractCrawler<string, string, ScrapeResult>
{
    private const string _authority = "http://synthetic";

    private readonly string[][] _links;
    private readonly string _robotsContent;

    public SyntheticCrawler(int pageCount, int fanout, string robotsContent, IOptions<CrawlerOptions> options)
        : base(options, NullLogger.Instance)
    {
        _robotsContent = robotsContent;
        _links = BuildGraph(pageCount, fanout);
    }

    public static string Entry => $"{_authority}/page/0";

    private static string[][] BuildGraph(int pageCount, int fanout)
    {
        var graph = new string[pageCount][];

        for (var i = 0; i < pageCount; i++)
        {
            var hrefs = new List<string>(fanout + 2);

            for (var k = 1; k <= fanout; k++)
            {
                var child = i * fanout + k;
                if (child < pageCount)
                    hrefs.Add($"{_authority}/page/{child}");
            }

            // Backlinks to already-seen pages so most emitted hrefs are duplicates,
            // stressing the dedup set and the per-dequeue MaxPages gate.
            hrefs.Add($"{_authority}/page/0");
            hrefs.Add($"{_authority}/page/{i / 2}");

            graph[i] = [.. hrefs];
        }

        return graph;
    }

    protected override Task<string?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(url);
    }

    protected override ValueTask<string> ParseResponse(string response)
    {
        return new ValueTask<string>(response);
    }

    protected override ValueTask<PageExtract> ExtractPageData(string document)
    {
        var lastSlash = document.LastIndexOf('/');
        var id = int.Parse(document.AsSpan(lastSlash + 1));

        var robots = IndexingHelper.ParseMetaRobots(_robotsContent);

        return ValueTask.FromResult(new PageExtract(null, robots, _links[id]));
    }

    protected override ValueTask<ScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new ScrapeResult { Urls = Visited });
    }
}
