using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Robots;

namespace SimpleCrawler.Core;

public abstract class AbstractRobotsCrawler<TResponse, TDocument, TResult> : AbstractCrawler<TResponse, TDocument, TResult>
    where TResult : IScrapeResult
{
    private readonly IRobotClient _robotClient;
    private readonly CrawlerOptions _options;
    private readonly ProductToken _productToken;
    private readonly ILogger _logger;

    private Dictionary<string, IRobotRuleChecker> _rulesByHost;
    private Dictionary<string, IRobotsTxt> _robotsByHost;
    private Dictionary<string, double> _delayByHost;

    protected AbstractRobotsCrawler(IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger logger, ICheckpointStore? checkpoint = null) : base(options, logger, checkpoint)
    {
        _robotClient = robotClient;
        _options = options.Value;
        _logger = logger;
        _productToken = ProductToken.Wildcard;

        _rulesByHost = new Dictionary<string, IRobotRuleChecker>(StringComparer.OrdinalIgnoreCase);
        _robotsByHost = new Dictionary<string, IRobotsTxt>(StringComparer.OrdinalIgnoreCase);
        _delayByHost = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        if (_options.BrowserProfile.UserAgent != null)
            _productToken = DeriveProductToken(_options.BrowserProfile.UserAgent);
    }

    private static ProductToken DeriveProductToken(string userAgent)
    {
        var span = userAgent.AsSpan().Trim();
        var end = span.IndexOfAny('/', ' ');
        var name = (end >= 0 ? span[..end] : span).ToString();

        return ProductToken.TryParse(name, out var token) ? token : ProductToken.Wildcard;
    }

    protected override async ValueTask InitializeCrawl(IReadOnlyList<string> entries, CancellationToken cancellationToken)
    {
        // Rules must be ready before base.InitializeCrawl enqueues entries (Enqueue consults IsCrawlAllowed),
        // so prime the site identities here and load each host's robots.txt before delegating. The scope is
        // the exact entry-authority set, so every host that can ever be crawled is already known.
        SetSiteIdentities(entries);

        _rulesByHost = new Dictionary<string, IRobotRuleChecker>(StringComparer.OrdinalIgnoreCase);
        _robotsByHost = new Dictionary<string, IRobotsTxt>(StringComparer.OrdinalIgnoreCase);
        _delayByHost = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var (authority, entryUri) in EntryUris)
        {
            var delay = _options.CrawlDelay;
            try
            {
                var robots = await _robotClient.LoadRobotsTxtAsync(entryUri, cancellationToken);
                _robotsByHost[authority] = robots;

                if (_options.RespectRobotsTxt && robots.TryGetCrawlDelay(_productToken, out var crawlDelay))
                    delay = Math.Max(delay, crawlDelay);

                _rulesByHost[authority] = robots.TryGetRules(_productToken, out var rules) ? rules : RobotRuleChecker.Empty;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // A single unreachable host must not abort a multi-host crawl; fall back to allowing the
                // host so its own fetches decide reachability.
                _logger.LogWarning("Could not load robots.txt for '{authority}': {message}", authority, e.Message);
                _rulesByHost[authority] = RobotRuleChecker.Empty;
            }

            _delayByHost[authority] = delay;
        }

        await base.InitializeCrawl(entries, cancellationToken);
    }

    protected override async ValueTask BackgroundDiscovery(CancellationToken cancellationToken)
    {
        if (!_options.EnableSitemapDiscovery)
            return;

        foreach (var (authority, entryUri) in EntryUris)
        {
            if (!_robotsByHost.TryGetValue(authority, out var robots))
                continue;

            try
            {
                var sitemap = robots.LoadSitemapAsync(entryUri, null, cancellationToken);
                await foreach (var item in sitemap)
                {
                    DiscoverLink(entryUri, item.Location.ToString());
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                _logger.LogWarning("Sitemap discovery failed for '{authority}': {message}", authority, e.Message);
            }
        }
    }

    protected override bool IsCrawlAllowed(string url)
    {
        if (!_options.RespectRobotsTxt)
            return true;

        var uri = new Uri(url);
        if (!_rulesByHost.TryGetValue(uri.Authority, out var rules))
            return true;

        return rules.IsAllowed(uri.PathAndQuery);
    }

    protected override double GetCrawlDelay(string authority)
    {
        return _delayByHost.TryGetValue(authority, out var delay) ? delay : _options.CrawlDelay;
    }
}
