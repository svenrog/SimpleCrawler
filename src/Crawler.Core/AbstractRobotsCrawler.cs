using Crawler.Core.Models;
using Crawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;

namespace Crawler.Core;

public abstract class AbstractRobotsCrawler<TResponse, TResult> : AbstractCrawler<TResponse, TResult>
    where TResult : IScrapeResult
{
    private readonly IRobotClient _robotClient;
    private readonly CrawlerOptions _options;
    private readonly ProductToken _userAgent;

    private IRobotRuleChecker? _robotRules;
    private IRobotsTxt? _robots;
    private Uri? _entryUri;

    protected AbstractRobotsCrawler(IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger logger) : base(options, logger)
    {
        _robotClient = robotClient;
        _options = options.Value;
        _userAgent = ProductToken.Wildcard;

        if (_options.UserAgent != null)
            _userAgent = ProductToken.Parse(_options.UserAgent);
    }

    protected override async ValueTask InitializeCrawl(string entry, CancellationToken cancellationToken)
    {
        _entryUri = new Uri(entry);
        _robots = await _robotClient.LoadRobotsTxtAsync(_entryUri, cancellationToken);

        if (_robots.TryGetCrawlDelay(_userAgent, out var crawlDelay) && _options.RespectRobotsTxt)
            _options.CrawlDelay = crawlDelay;

        if (!_robots.TryGetRules(_userAgent, out _robotRules))
            _robotRules = RobotRuleChecker.Empty;

        await base.InitializeCrawl(entry, cancellationToken);
    }

    protected override async ValueTask BackgroundDiscovery(CancellationToken cancellationToken)
    {
        var sitemap = _robots!.LoadSitemapAsync(_entryUri!, null, cancellationToken);
        await foreach (var item in sitemap)
        {
            var url = item.Location.ToString();

            DiscoverLink(url);
        }
    }

    protected override bool InvalidateHref([NotNullWhen(false)] string? href)
    {
        if (base.InvalidateHref(href))
            return true;

        if (!_options.RespectRobotsTxt)
            return false;

        return !_robotRules!.IsAllowed(href);
    }
}
