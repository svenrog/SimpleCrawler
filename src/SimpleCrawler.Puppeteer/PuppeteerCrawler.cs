using SimpleCrawler.Core;
using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using System.Collections.Concurrent;
using System.Text.Json;

namespace SimpleCrawler.Puppeteer;

public abstract class PuppeteerCrawler<TResult> : AbstractRobotsCrawler<IPage, IPage, TResult>
    where TResult : IScrapeResult
{
    private readonly PuppeteerBrowserSession _session;
    private readonly ProxyRetryExecutor? _retry;
    private readonly ConcurrentDictionary<string, ConcurrentQueue<IPage>> _pagePools;
    private readonly ConcurrentDictionary<IPage, string> _pageKeys;
    private readonly ILogger _logger;

    protected PuppeteerCrawler(IRobotClient robotClient, PuppeteerBrowserSession session, IOptions<HeadlessCrawlerOptions> options, ILogger logger, IProxyPool? pool = null) : base(robotClient, options, logger)
    {
        _session = session;
        _retry = options.Value.ProxyPool is not null && pool is not null
            ? new ProxyRetryExecutor(pool, options.Value.ProxyPool.MaxRetries)
            : null;
        _logger = logger;
        _pagePools = new ConcurrentDictionary<string, ConcurrentQueue<IPage>>();
        _pageKeys = new ConcurrentDictionary<IPage, string>();
    }

    protected override async Task<IPage?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        if (_retry is null)
            return await LoadOnce(url, null, cancellationToken);

        return await _retry.ExecuteAsync(
            (proxy, token) => AttemptLoad(url, proxy, token),
            () =>
            {
                _logger.LogWarning("Exhausted proxy retries for '{url}'", url);
                return (IPage?)null;
            },
            cancellationToken);
    }

    private async Task<ProxyAttempt<IPage?>> AttemptLoad(string url, ProxyInfo? proxy, CancellationToken cancellationToken)
    {
        var page = await AcquirePage(proxy);

        IResponse? response;
        try
        {
            response = await page.GoToAsync(url, GetNavigationOptions()).WaitAsync(cancellationToken);
        }
        catch (PuppeteerException e)
        {
            _logger.LogDebug("Proxy {proxy} failed on '{url}': {message}", proxy, url, e.Message);
            await ClosePage(page);
            return ProxyAttempt<IPage?>.Failed(ProxyFailureKind.Connection);
        }

        if (response is null)
        {
            _logger.LogWarning("No response from '{url}' via proxy {proxy}", url, proxy);
            await ClosePage(page);
            return ProxyAttempt<IPage?>.Failed(ProxyFailureKind.Connection);
        }

        var status = (int)response.Status;
        var kind = ProxyFailureClassifier.Classify(status);
        if (kind is not null)
        {
            _logger.LogDebug("Proxy {proxy} returned {code} on '{url}'", proxy, status, url);
            await ClosePage(page);
            return ProxyAttempt<IPage?>.Failed(kind.Value);
        }

        _pageKeys[page] = BrowserProxyHelper.ContextKey(proxy);

        if (status < 300)
        {
            _logger.LogDebug("Response '{code}' from url '{url}' via proxy {proxy}", status, url, proxy);
            return ProxyAttempt<IPage?>.Ok(page);
        }

        _logger.LogWarning("Error {code} on url '{url}'", status, url);
        await DisposeResponse(page);
        return ProxyAttempt<IPage?>.Ok(null);
    }

    private async Task<IPage?> LoadOnce(string url, ProxyInfo? proxy, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var page = await AcquirePage(proxy);

        var response = await page.GoToAsync(url, GetNavigationOptions()).WaitAsync(cancellationToken);
        if (response == null)
        {
            _logger.LogWarning("No response from '{url}'", url);
            await DisposeResponse(page);

            return null;
        }
        else if ((int)response.Status < 300)
        {
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
        catch (PuppeteerException)
        {
        }
    }

    protected virtual NavigationOptions GetNavigationOptions()
    {
        return Constants.DefaultNavigationOptions;
    }

    protected override ValueTask<IPage> ParseResponse(IPage response)
    {
        return new ValueTask<IPage>(response);
    }

    protected override async ValueTask<PageExtract> ExtractPageData(IPage response)
    {
        var json = await response.EvaluateFunctionAsync<JsonElement>(RenderedPageExtractor.Script).WaitAsync(CrawlCancellationToken);
        var (canonicalHref, robotsContent, linkHrefs) = RenderedPageExtractor.Parse(json);

        return new PageExtract(canonicalHref, IndexingHelper.ParseMetaRobots(robotsContent), linkHrefs);
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
