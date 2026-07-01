using Crawler.Core.Models;
using Crawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawler.Core;

public abstract class AbstractRobotsCrawler<TResponse, TResult> : AbstractCrawler<TResponse, TResult>
    where TResult : IScrapeResult
{
    private readonly IRobotClient _robotClient;
    private readonly CrawlerOptions _options;
    private readonly ProductToken _userAgent;
    private readonly ILogger _logger;

    private IRobotRuleChecker? _robotRules;
    private IRobotsTxt? _robots;
    private Uri? _entryUri;
    private string? _siteAuthority;

    protected AbstractRobotsCrawler(IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger logger) : base(options, logger)
    {
        _robotClient = robotClient;
        _options = options.Value;
        _logger = logger;
        _userAgent = ProductToken.Wildcard;

        if (_options.UserAgent != null)
            _userAgent = ProductToken.Parse(_options.UserAgent);
    }

    protected override async ValueTask InitializeCrawl(string entry, CancellationToken cancellationToken)
    {
        _entryUri = new Uri(entry);
        _siteAuthority = _entryUri.GetLeftPart(UriPartial.Authority);
        _robots = await _robotClient.LoadRobotsTxtAsync(_entryUri, cancellationToken);

        if (_robots.TryGetCrawlDelay(_userAgent, out var crawlDelay) && _options.RespectRobotsTxt)
            _options.CrawlDelay = crawlDelay;

        if (!_robots.TryGetRules(_userAgent, out _robotRules))
            _robotRules = RobotRuleChecker.Empty;

        await base.InitializeCrawl(entry, cancellationToken);
    }

    protected override async ValueTask BackgroundDiscovery(CancellationToken cancellationToken)
    {
        if (!_options.EnableSitemapDiscovery)
            return;

        try
        {
            var sitemap = _robots!.LoadSitemapAsync(_entryUri!, null, cancellationToken);
            await foreach (var item in sitemap)
            {
                var url = item.Location.ToString();

                DiscoverLink(url);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning("Sitemap discovery failed: {message}", e.Message);
        }
    }

    protected override bool IsCrawlAllowed(string url)
    {
        if (!_options.RespectRobotsTxt)
            return true;

        return _robotRules!.IsAllowed(GetSitePath(url));
    }

    private string GetSitePath(string url)
    {
        var authority = _siteAuthority!;
        if (url.Length > authority.Length && url.StartsWith(authority, StringComparison.OrdinalIgnoreCase))
            return url[authority.Length..];

        return "/";
    }
}
