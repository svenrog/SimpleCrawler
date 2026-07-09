using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Extensions;
using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Core.Retry;
using SimpleCrawler.Core.Robots;
using System.Collections.Concurrent;
using System.Text.Json;

namespace SimpleCrawler.Core;

public abstract class AbstractHeadlessCrawler<TPage, TResult> : AbstractRobotsCrawler<TPage, TPage, TResult>
    where TPage : class
    where TResult : IScrapeResult
{
    private readonly RetryExecutor _retry;
    private readonly ConcurrentDictionary<string, ConcurrentQueue<TPage>> _pagePools;
    private readonly ConcurrentDictionary<TPage, string> _pageKeys;
    private readonly ILogger _logger;

    protected AbstractHeadlessCrawler(IRobotClient robotClient, IOptions<HeadlessCrawlerOptions> options, ILogger logger, IProxyPool? pool = null, ICheckpointStore? checkpoint = null) : base(robotClient, options, logger, checkpoint)
    {
        _retry = new RetryExecutor(options.Value.Retry, pool);
        _pagePools = new ConcurrentDictionary<string, ConcurrentQueue<TPage>>();
        _pageKeys = new ConcurrentDictionary<TPage, string>();
        _logger = logger;
    }

    protected override async Task<TPage?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        return await _retry.ExecuteAsync(
            (proxy, token) => AttemptLoad(url, proxy, token),
            () =>
            {
                _logger.LogWarning("Exhausted retries for '{url}'", url);
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

        ReportResponse(url, status.Value, null, null);

        var reason = RetryClassifier.Classify(status.Value);
        if (reason is not null)
        {
            _logger.LogDebug("Proxy {proxy} returned {code} on '{url}'", proxy, status.Value, url);
            await ClosePage(page);
            return RetryAttempt<TPage?>.Failed(reason.Value);
        }

        _pageKeys[page] = BrowserProxyHelper.ContextKey(proxy);

        if (status.Value.IsSuccessStatus())
        {
            await AfterSuccessfulLoad(page, cancellationToken);
            _logger.LogDebug("Response '{code}' from url '{url}' via proxy {proxy}", status.Value, url, proxy);
            return RetryAttempt<TPage?>.Ok(page);
        }

        _logger.LogWarning("Error {code} on url '{url}'", status.Value, url);
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

    protected abstract Task<TPage> NewPageAsync(ProxyInfo? proxy);

    protected abstract Task<int?> NavigateAsync(TPage page, string url, ProxyInfo? proxy, CancellationToken cancellationToken);

    protected abstract Task ClosePageCore(TPage page);

    protected abstract Task<JsonElement> EvaluateExtractorAsync(TPage page, string script, CancellationToken cancellationToken);

    protected virtual Task AfterSuccessfulLoad(TPage page, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
