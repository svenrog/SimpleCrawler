using SimpleCrawler.Core;
using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using System.Collections.Concurrent;

namespace SimpleCrawler.Playwright;

public abstract class PlaywrightCrawler<TResult> : AbstractRobotsCrawler<IPage, IPage, TResult>
    where TResult : IScrapeResult
{
    private readonly PlaywrightBrowserSession _session;
    private readonly HeadlessCrawlerOptions _options;
    private readonly IProxyPool? _pool;
    private readonly int _maxRetries;
    private readonly ILogger _logger;

    private readonly ConcurrentDictionary<string, ConcurrentQueue<IPage>> _pagePools;
    private readonly ConcurrentDictionary<IPage, string> _pageKeys;

    protected PlaywrightCrawler(IRobotClient robotClient, PlaywrightBrowserSession session, IOptions<HeadlessCrawlerOptions> options, ILogger logger, IProxyPool? pool = null) : base(robotClient, options, logger)
    {
        _session = session;
        _options = options.Value;
        _pool = _options.ProxyPool is not null ? pool : null;
        _maxRetries = _options.ProxyPool?.MaxRetries ?? 0;
        _logger = logger;
        _pagePools = new ConcurrentDictionary<string, ConcurrentQueue<IPage>>();
        _pageKeys = new ConcurrentDictionary<IPage, string>();
    }

    protected override async Task<IPage?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        if (_pool is null)
            return await LoadOnce(url, null);

        for (var attempt = 0; attempt <= _maxRetries; attempt++)
        {
            var proxy = _pool.Acquire() ?? throw new ProxyPoolExhaustedException("No healthy proxies remain (below configured cutoff).");
            var page = await AcquirePage(proxy);

            IResponse? response;
            try
            {
                response = await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.Load });
            }
            catch (PlaywrightException e)
            {
                _logger.LogDebug("Proxy {proxy} failed on '{url}' (attempt {attempt}): {message}", proxy, url, attempt + 1, e.Message);
                _pool.ReportFailure(proxy, ProxyFailureKind.Connection);
                await ClosePage(page);
                continue;
            }

            if (response is null)
            {
                _logger.LogWarning("No response from '{url}' via proxy {proxy}", url, proxy);
                _pool.ReportFailure(proxy, ProxyFailureKind.Connection);
                await ClosePage(page);
                continue;
            }

            var kind = ProxyFailureClassifier.Classify(response.Status);
            if (kind is not null)
            {
                _logger.LogDebug("Proxy {proxy} returned {code} on '{url}' (attempt {attempt})", proxy, response.Status, url, attempt + 1);
                _pool.ReportFailure(proxy, kind.Value);
                await ClosePage(page);
                continue;
            }

            _pool.ReportSuccess(proxy);

            if (response.Status is >= 200 and < 300)
            {
                await WaitForNetworkIdle(page);
                _logger.LogDebug("Response '{code}' from url '{url}' via proxy {proxy}", response.Status, url, proxy);
                _pageKeys[page] = BrowserProxyHelper.ContextKey(proxy);
                return page;
            }

            _logger.LogWarning("Error {code} on url '{url}'", response.Status, url);
            _pageKeys[page] = BrowserProxyHelper.ContextKey(proxy);
            await DisposeResponse(page);
            return null;
        }

        _logger.LogWarning("Exhausted proxy retries for '{url}'", url);
        return null;
    }

    private async Task<IPage?> LoadOnce(string url, ProxyInfo? proxy)
    {
        var page = await AcquirePage(proxy);
        var response = await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

        if (response == null)
        {
            _logger.LogWarning("No response from '{url}'", url);
            await DisposeResponse(page);

            return null;
        }
        else if (response.Status >= 200 && response.Status < 300)
        {
            await WaitForNetworkIdle(page);
            _logger.LogDebug("Response '{code}' from url '{url}'", response.Status, url);
            return page;
        }
        else
        {
            _logger.LogWarning("Error {code} on url '{url}'", response.Status, url);
            await DisposeResponse(page);

            return null;
        }
    }

    protected override ValueTask<IPage> ParseResponse(IPage response)
    {
        return new ValueTask<IPage>(response);
    }

    protected override async ValueTask<PageExtract> ExtractPageData(IPage response)
    {
        var json = await response.EvaluateAsync(RenderedPageExtractor.Script);
        var (canonicalHref, robotsContent, linkHrefs) = RenderedPageExtractor.Parse(json.GetValueOrDefault());

        return new PageExtract(canonicalHref, IndexingHelper.ParseMetaRobots(robotsContent), linkHrefs);
    }

    private async Task WaitForNetworkIdle(IPage page)
    {
        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = _options.NetworkIdleGraceMs });
        }
        catch (System.TimeoutException)
        {
        }
    }

    private async ValueTask<IPage> AcquirePage(ProxyInfo? proxy)
    {
        var key = BrowserProxyHelper.ContextKey(proxy);
        if (_pagePools.TryGetValue(key, out var queue) && queue.TryDequeue(out var page))
            return page;

        return await _session.NewPageAsync(proxy);
    }

    private async Task ClosePage(IPage page)
    {
        _pageKeys.TryRemove(page, out _);

        try
        {
            await page.CloseAsync();
        }
        catch (PlaywrightException)
        {
        }
    }

    protected override Task DisposeResponse(IPage? response)
    {
        if (response == null)
            return Task.CompletedTask;

        var key = _pageKeys.TryRemove(response, out var stored) ? stored : string.Empty;
        var queue = _pagePools.GetOrAdd(key, _ => new ConcurrentQueue<IPage>());
        queue.Enqueue(response);

        return Task.CompletedTask;
    }
}
