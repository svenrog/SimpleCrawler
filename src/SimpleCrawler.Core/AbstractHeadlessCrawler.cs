using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Collectors;
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

    protected AbstractHeadlessCrawler(IRobotClient robotClient, IOptions<HeadlessCrawlerOptions> options, ILogger logger, IProxyPool? pool = null, ICheckpointStore? checkpoint = null, IEnumerable<ICrawlCollector>? collectors = null) : base(robotClient, options, logger, checkpoint, collectors)
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
        // A first-use browser launch was uncancellable here.
        var page = await AcquirePage(proxy).AsTask().WaitAsync(cancellationToken);

        (int? Status, IReadOnlyDictionary<string, string>? Headers) navigation;
        try
        {
            navigation = await NavigateAsync(page, url, proxy, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await ClosePage(page);
            throw;
        }

        if (navigation.Status is not { } status)
        {
            await ClosePage(page);
            return RetryAttempt<TPage?>.Failed(RetryReason.Connection);
        }

        ReportResponse(url, ToResponseSignal(status, navigation.Headers));

        var reason = RetryClassifier.Classify(status);
        if (reason is not null)
        {
            _logger.LogDebug("Retryable '{code}' from '{url}' via '{proxy}'", status, url, ProxyLabel.Describe(proxy));
            await ClosePage(page);
            return RetryAttempt<TPage?>.Failed(reason.Value);
        }

        _pageKeys[page] = BrowserProxyHelper.ContextKey(proxy);

        if (status.IsSuccessStatus())
        {
            await AfterSuccessfulLoad(page, cancellationToken);
            _logger.LogDebug("Response '{code}' from '{url}'", status, url);
            return RetryAttempt<TPage?>.Ok(page);
        }

        _logger.LogWarning("Error '{code}' from '{url}'", status, url);
        await DisposeResponse(page);
        return RetryAttempt<TPage?>.Ok(null);
    }

    /// <summary>
    /// Normalizes a headless navigation result into a <see cref="ResponseSignal"/>. The headless
    /// backends surface no content length/type, so only status and (when captured) the header-derived
    /// signals are populated. Headers arrive already lower-cased and newline-joined per repeated name;
    /// cookie names are split back out of <c>Set-Cookie</c>.
    /// </summary>
    private ResponseSignal ToResponseSignal(int status, IReadOnlyDictionary<string, string>? headers)
    {
        if (!CaptureSignals || headers is null)
            return new ResponseSignal { StatusCode = status };

        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in headers)
            normalized[key.ToLowerInvariant()] = value;

        var cookieNames = new List<string>();
        if (normalized.TryGetValue("set-cookie", out var setCookie))
        {
            foreach (var line in setCookie.Split('\n'))
            {
                var pair = line.Split(';', 2)[0];
                var equals = pair.IndexOf('=', StringComparison.Ordinal);
                if (equals > 0)
                    cookieNames.Add(pair[..equals].Trim());
            }
        }

        return new ResponseSignal { StatusCode = status, Headers = normalized, CookieNames = cookieNames };
    }

    protected override ValueTask<TPage> ParseResponse(TPage response)
    {
        return new ValueTask<TPage>(response);
    }

    protected override async ValueTask<PageExtract> ExtractPageData(TPage response)
    {
        var json = await EvaluateExtractorAsync(response, RenderedPageExtractor.Script, CrawlCancellationToken);
        if (string.IsNullOrEmpty(json))
            return new PageExtract(null, RobotsRules.All, [], CaptureSignals ? new PageSignals() : null);

        using var document = JsonDocument.Parse(json);
        var (canonicalHref, robotsContent, linkHrefs, signals) = RenderedPageExtractor.Parse(document.RootElement);

        return new PageExtract(canonicalHref, IndexingHelper.ParseMetaRobots(robotsContent), linkHrefs, signals);
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

    protected abstract Task<(int? Status, IReadOnlyDictionary<string, string>? Headers)> NavigateAsync(TPage page, string url, ProxyInfo? proxy, CancellationToken cancellationToken);

    protected abstract Task ClosePageCore(TPage page);

    protected abstract Task<string?> EvaluateExtractorAsync(TPage page, string script, CancellationToken cancellationToken);

    protected virtual Task AfterSuccessfulLoad(TPage page, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
