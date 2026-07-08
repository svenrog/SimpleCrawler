using SimpleCrawler.Core.Robots;
using System.Diagnostics.CodeAnalysis;

namespace SimpleCrawler.Tests.Helpers;

// Minimal in-memory robots client that reports no crawl-delay and no rules, so tests exercise crawler
// behaviour (throttling, checkpointing) without a live robots.txt.
public sealed class StubRobotClient : IRobotClient
{
    public Task<IRobotsTxt> LoadRobotsTxtAsync(Uri url, CancellationToken cancellationToken = default)
        => Task.FromResult<IRobotsTxt>(new StubRobotsTxt());

    public IAsyncEnumerable<UrlSetItem> LoadSitemapsAsync(Uri uri, DateTime? modifiedSince = null, CancellationToken cancellationToken = default)
        => Empty();

    private static async IAsyncEnumerable<UrlSetItem> Empty()
    {
        await Task.CompletedTask;
        yield break;
    }

    private sealed class StubRobotsTxt : IRobotsTxt
    {
        public bool TryGetCrawlDelay(ProductToken userAgent, out int crawlDelay)
        {
            crawlDelay = 0;
            return false;
        }

        public bool TryGetRules(ProductToken userAgent, [NotNullWhen(true)] out IRobotRuleChecker? ruleChecker)
        {
            ruleChecker = null;
            return false;
        }

        public bool TryGetHost([NotNullWhen(true)] out string? host)
        {
            host = null;
            return false;
        }

        public IAsyncEnumerable<UrlSetItem> LoadSitemapAsync(Uri url, DateTime? modifiedSince = default, CancellationToken cancellationToken = default)
            => Empty();
    }
}
