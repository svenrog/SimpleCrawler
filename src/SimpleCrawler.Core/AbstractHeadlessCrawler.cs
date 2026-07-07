using SimpleCrawler.Core.Extensions;
using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Core.Retry;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;

namespace SimpleCrawler.Core;

// Shared orchestration for headless (real-browser) backends: page-pool lifecycle keyed by proxy
// context, the acquire/report/retry loop, and the single-evaluation page extraction. Each backend
// (Playwright, Puppeteer) supplies only the vendor-specific primitives - open a page, navigate,
// close it, run the extractor script - so the fetch pipeline lives in exactly one place.
public abstract class AbstractHeadlessCrawler<TPage, TResult> : AbstractRobotsCrawler<TPage, TPage, TResult>
    where TPage : class
    where TResult : IScrapeResult
{
    private readonly RetryExecutor _retry;
    private readonly ConcurrentDictionary<string, ConcurrentQueue<TPage>> _pagePools;
    private readonly ConcurrentDictionary<TPage, string> _pageKeys;

    protected ILogger Logger { get; }

    protected AbstractHeadlessCrawler(IRobotClient robotClient, IOptions<HeadlessCrawlerOptions> options, ILogger logger, IProxyPool? pool = null) : base(robotClient, options, logger)
    {
        _retry = new RetryExecutor(options.Value.Retry, pool);
        _pagePools = new ConcurrentDictionary<string, ConcurrentQueue<TPage>>();
        _pageKeys = new ConcurrentDictionary<TPage, string>();
        Logger = logger;
    }

    protected override async Task<TPage?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        return await _retry.ExecuteAsync(
            (proxy, token) => AttemptLoad(url, proxy, token),
            () =>
            {
                Logger.LogWarning("Exhausted retries for '{url}'", url);
                return (TPage?)null;
            },
            cancellationToken);
    }

    private async Task<RetryAttempt<TPage?>> AttemptLoad(string url, ProxyInfo? proxy, CancellationToken cancellationToken)
    {
        var page = await AcquirePage(proxy);

        int? status;
        try
        {
            status = await NavigateAsync(page, url, proxy, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await ClosePage(page);
            throw;
        }

        if (status is null)
        {
            await ClosePage(page);
            return RetryAttempt<TPage?>.Failed(RetryReason.Connection);
        }

        var reason = RetryClassifier.Classify(status.Value);
        if (reason is not null)
        {
            Logger.LogDebug("Proxy {proxy} returned {code} on '{url}'", proxy, status.Value, url);
            await ClosePage(page);
            return RetryAttempt<TPage?>.Failed(reason.Value);
        }

        _pageKeys[page] = BrowserProxyHelper.ContextKey(proxy);

        if (status.Value.IsSuccessStatus())
        {
            await AfterSuccessfulLoad(page, cancellationToken);
            Logger.LogDebug("Response '{code}' from url '{url}' via proxy {proxy}", status.Value, url, proxy);
            return RetryAttempt<TPage?>.Ok(page);
        }

        Logger.LogWarning("Error {code} on url '{url}'", status.Value, url);
        await DisposeResponse(page);
        return RetryAttempt<TPage?>.Ok(null);
    }

    protected override ValueTask<TPage> ParseResponse(TPage response)
    {
        return new ValueTask<TPage>(response);
    }

    protected override async ValueTask<PageExtract> ExtractPageData(TPage response)
    {
        var json = await EvaluateExtractorAsync(response, RenderedPageExtractor.Script, CrawlCancellationToken);
        var (canonicalHref, robotsContent, linkHrefs) = RenderedPageExtractor.Parse(json);

        return new PageExtract(canonicalHref, IndexingHelper.ParseMetaRobots(robotsContent), linkHrefs);
    }

    private async ValueTask<TPage> AcquirePage(ProxyInfo? proxy)
    {
        var key = BrowserProxyHelper.ContextKey(proxy);
        if (_pagePools.TryGetValue(key, out var queue) && queue.TryDequeue(out var page))
            return page;

        return await NewPageAsync(proxy);
    }

    private async Task ClosePage(TPage page)
    {
        _pageKeys.TryRemove(page, out _);
        await ClosePageCore(page);
    }

    protected override Task DisposeResponse(TPage? response)
    {
        if (response == null)
            return Task.CompletedTask;

        var key = _pageKeys.TryRemove(response, out var stored) ? stored : string.Empty;
        var queue = _pagePools.GetOrAdd(key, _ => new ConcurrentQueue<TPage>());
        queue.Enqueue(response);

        return Task.CompletedTask;
    }

    // Opens a fresh page routed through the given proxy (null = direct).
    protected abstract Task<TPage> NewPageAsync(ProxyInfo? proxy);

    // Navigates the page to the URL and returns the HTTP status, or null for a connection-level
    // failure (the implementation logs the specific cause). Must throw OperationCanceledException on
    // cancellation so the base can close the page and propagate.
    protected abstract Task<int?> NavigateAsync(TPage page, string url, ProxyInfo? proxy, CancellationToken cancellationToken);

    // Closes a page, swallowing vendor-specific teardown exceptions.
    protected abstract Task ClosePageCore(TPage page);

    // Runs the extraction script in the page and returns its JSON result.
    protected abstract Task<JsonElement> EvaluateExtractorAsync(TPage page, string script, CancellationToken cancellationToken);

    // Optional post-load settling (e.g. waiting for network idle) once a page loads successfully.
    protected virtual Task AfterSuccessfulLoad(TPage page, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
