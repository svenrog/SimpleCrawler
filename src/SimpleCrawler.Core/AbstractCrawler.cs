using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Collections;
using SimpleCrawler.Core.Comparers;
using SimpleCrawler.Core.Extensions;
using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Progress;
using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Core.Throttling;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace SimpleCrawler.Core;

/// <summary>
/// Drives the two-stage crawl pipeline: an unbounded URL channel feeds fetch workers, which hand
/// responses to a bounded parse channel feeding parse workers. Each enqueued URL is tracked by an
/// outstanding-count that is decremented when the URL leaves the system (fetch failure or parse
/// completion); when it reaches zero both channels are completed and the crawl drains. Subclasses
/// supply the backend-specific load/parse/extract hooks and, via the intermediate abstract layers,
/// robots.txt, sitemap discovery, and per-document analysis.
/// </summary>
public abstract class AbstractCrawler<TResponse, TDocument, TResult> : ICrawler<TResult>
    where TResult : IScrapeResult
{
    private readonly CrawlerOptions _options;
    private readonly AdaptiveThrottler _throttling;
    private readonly CheckpointCoordinator? _checkpoints;
    private readonly ILogger _logger;

    private CrawlState _state;

    private Channel<string> _urlChannel;
    private Channel<(string Url, TResponse Response)> _parseChannel;

    private HashSet<string> _scopeAuthorities;
    private Dictionary<string, Uri> _entryByAuthority;

    private int _outstanding;
    private int _processedCount;
    private int _aborted;

    protected AdaptiveThrottler Throttling => _throttling;
    protected IReadOnlyDictionary<string, Uri> EntryUris => _entryByAuthority;
    protected ConcurrentHashSet<string> Visited => _state.Visited;
    protected IReadOnlyCollection<UrlReport> Reports => [.. _state.Reports.Values];

    protected CancellationToken CrawlCancellationToken { get; private set; }

    protected AbstractCrawler(IOptions<CrawlerOptions> options, ILogger logger, ICheckpointStore? checkpoint = null)
    {
        _options = options.Value;
        _logger = logger;
        _throttling = new AdaptiveThrottler(_options.Throttling, logger);
        _checkpoints = checkpoint is not null ? new CheckpointCoordinator(checkpoint, _options.Checkpoint.Interval, logger) : null;

        _scopeAuthorities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _entryByAuthority = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
        _state = new CrawlState();

        _urlChannel = CreateUrlChannel();
        _parseChannel = CreateParseChannel();
    }

    public virtual Task<TResult> Start(string entry, CancellationToken cancellationToken = default)
    {
        return Start([entry], cancellationToken);
    }

    public virtual async Task<TResult> Start(IReadOnlyList<string> entries, CancellationToken cancellationToken = default)
    {
        CrawlCancellationToken = cancellationToken;

        await InitializeCrawl(entries, cancellationToken);

        Interlocked.Increment(ref _outstanding);

        using var autosave = StartAutosave(cancellationToken);
        using var progress = StartProgress(cancellationToken);

        var tasks = BuildWorkerTasks(cancellationToken);

        try
        {
            await Task.WhenAll(tasks);
        }
        finally
        {
            await progress.StopAsync();
            await autosave.StopAsync();

            if (_checkpoints is not null)
                await _checkpoints.SaveAsync(_state, CancellationToken.None);
        }

        return await GetResult(cancellationToken);
    }

    /// <summary>
    /// Builds the crawl's worker tasks: one background-discovery task that seeds the frontier, plus the
    /// fetch and parse worker pools sized by the effective concurrency options.
    /// </summary>
    private Task[] BuildWorkerTasks(CancellationToken cancellationToken)
    {
        var fetchCount = _options.EffectiveConcurrency;
        var parseCount = _options.EffectiveParseConcurrency;

        var tasks = new Task[1 + fetchCount + parseCount];
        tasks[0] = Task.Run(() => RunDiscovery(cancellationToken), cancellationToken);

        var index = 1;
        for (var i = 0; i < fetchCount; i++)
            tasks[index++] = RunFetchWorker(cancellationToken);
        for (var i = 0; i < parseCount; i++)
            tasks[index++] = RunParseWorker(cancellationToken);

        return tasks;
    }

    /// <summary>
    /// Runs background sitemap discovery, then balances the outstanding-count seeded for it in Start so
    /// the crawl can complete once discovery and all enqueued URLs have drained.
    /// </summary>
    private async Task RunDiscovery(CancellationToken cancellationToken)
    {
        try
        {
            await BackgroundDiscovery(cancellationToken);
        }
        finally
        {
            CompleteUrl();
        }
    }

    /// <summary>
    /// Starts the checkpoint autosave loop as a background sidecar, or returns an idle one when
    /// checkpointing is disabled.
    /// </summary>
    private BackgroundOperation StartAutosave(CancellationToken cancellationToken)
    {
        if (_checkpoints is null)
            return BackgroundOperation.None();

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        return BackgroundOperation.Start(cts, _checkpoints.RunAutosaveAsync(() => _state, cts.Token));
    }

    /// <summary>
    /// Starts the progress reporter as a background sidecar, or returns an idle one when progress
    /// reporting is disabled.
    /// </summary>
    private BackgroundOperation StartProgress(CancellationToken cancellationToken)
    {
        if (!_options.Progress.Enabled)
            return BackgroundOperation.None();

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // Sample the cumulative Processed count (not the run-local _processedCount, which resets to 0 on a
        // checkpoint resume) so the pending frontier and yield stay correct across a resumed crawl.
        var task = new CrawlProgressReporter(_options.Progress, _logger).RunAsync(
            () => (_state.Processed.Count, _state.Discovered.Count), _options.MaxPages, cts.Token);
        return BackgroundOperation.Start(cts, task);
    }

    /// <summary>
    /// Fully resets per-crawl state so a single crawler instance can be reused across Start calls.
    /// </summary>
    protected virtual async ValueTask InitializeCrawl(IReadOnlyList<string> entries, CancellationToken cancellationToken)
    {
        SetSiteIdentities(entries);

        _urlChannel = CreateUrlChannel();
        _parseChannel = CreateParseChannel();
        _outstanding = 0;
        _processedCount = 0;
        _aborted = 0;

        _throttling.Reset(_scopeAuthorities);

        _checkpoints?.LogEnabled();

        var restored = _checkpoints is not null ? await _checkpoints.LoadAsync(entries, cancellationToken) : null;
        _state = restored ?? new CrawlState(entries);

        if (restored is not null)
        {
            EnqueuePending();
            return;
        }

        foreach (var entry in entries)
            Enqueue(entry);
    }

    /// <summary>
    /// Re-queues the frontier left by a restored checkpoint: every discovered URL that was not yet processed.
    /// </summary>
    private void EnqueuePending()
    {
        var pending = 0;
        foreach (var url in _state.Discovered)
        {
            if (!_state.Processed.Contains(url) && EnqueueKnownUrl(url))
                pending++;
        }

        _logger.LogInformation("Resuming from checkpoint: {processed} processed, {pending} pending.",
            _state.Processed.Count, pending);
    }

    protected void SetSiteIdentities(IReadOnlyList<string> entries)
    {
        _scopeAuthorities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _entryByAuthority = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var entryUri = new Uri(entry);
            var authority = entryUri.Authority;

            if (_scopeAuthorities.Add(authority))
                _entryByAuthority[authority] = new Uri(entryUri.GetLeftPart(UriPartial.Authority));
        }
    }

    private static Channel<string> CreateUrlChannel()
    {
        var options = new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
        };
        return Channel.CreateUnbounded<string>(options);
    }

    private Channel<(string Url, TResponse Response)> CreateParseChannel()
    {
        var options = new BoundedChannelOptions(_options.EffectiveConcurrency)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        };
        return Channel.CreateBounded<(string, TResponse)>(options);
    }

    /// <summary>
    /// A URL leaves the system when its fetch fails or its parse completes; when none remain in flight,
    /// both stages are drained and safe to complete (parse workers only block on read; only fetch workers
    /// block on the bounded parse-channel write, which a non-empty system always drains).
    /// </summary>
    private void CompleteUrl()
    {
        if (Interlocked.Decrement(ref _outstanding) == 0)
        {
            _urlChannel.Writer.TryComplete();
            _parseChannel.Writer.TryComplete();
        }
    }

    /// <summary>
    /// Soft-abort: stop scheduling new fetches (completing the URL channel makes Enqueue a no-op and
    /// drains fetch workers) while letting in-flight parses finish, so Start returns partial results.
    /// </summary>
    protected void Abort(string reason)
    {
        if (Interlocked.CompareExchange(ref _aborted, 1, 0) != 0)
            return;

        _logger.LogCritical("Aborting crawl: {reason}.", reason);
        _urlChannel.Writer.TryComplete();
    }

    /// <summary>
    /// Records a URL as fully processed (parse done, or a terminal fetch failure) and bumps the MaxPages
    /// counter. Distinct from CompleteUrl, which balances the outstanding-count tracking the URL through
    /// the pipeline; both fire together as a URL exits.
    /// </summary>
    private void MarkProcessed(string url)
    {
        _state.Processed.Add(url);
        Interlocked.Increment(ref _processedCount);
    }

    private async Task RunFetchWorker(CancellationToken cancellationToken)
    {
        var reader = _urlChannel.Reader;

        while (await reader.WaitToReadAsync(cancellationToken))
        {
            while (reader.TryRead(out var url))
                await FetchOneAsync(url, cancellationToken);
        }
    }

    /// <summary>
    /// Fetches a single dequeued URL: throttles, loads the response, and either hands it to the parse
    /// channel or finalizes a failure. Skipped URLs (MaxPages reached, or aborted) still balance their
    /// outstanding-count via CompleteUrl so the crawl can drain.
    /// </summary>
    private async Task FetchOneAsync(string url, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _processedCount) >= _options.MaxPages)
        {
            CompleteUrl();
            return;
        }

        if (Volatile.Read(ref _aborted) == 1)
        {
            CompleteUrl();
            return;
        }

        var entry = new UrlReport { Url = url, Timestamp = DateTimeOffset.UtcNow };
        _state.Reports[url] = entry;

        var handedOff = false;
        try
        {
            var authority = new Uri(url).Authority;
            await _throttling.WaitAsync(authority, GetCrawlDelay(authority), cancellationToken);

            var startTimestamp = Stopwatch.GetTimestamp();
            var response = await LoadResponse(url, cancellationToken);
            entry.FetchDuration = Stopwatch.GetElapsedTime(startTimestamp);

            if (response != null)
            {
                await _parseChannel.Writer.WriteAsync((url, response), cancellationToken);
                handedOff = true;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ProxyPoolExhaustedException)
        {
            entry.Outcome = CrawlOutcome.Aborted;
            Abort("proxy pool exhausted");
        }
        catch (TimeoutException ex)
        {
            entry.Outcome = CrawlOutcome.Timeout;
            entry.Error = ex.Message;
            _logger.LogWarning("Timeout fetching '{url}': {message}", url, ex.Message);
        }
        catch (Exception ex)
        {
            entry.Outcome = CrawlOutcome.FetchError;
            entry.Error = ex.Message;
            _logger.LogError(ex, "Encountered error fetching '{url}': {message}", url, ex.Message);
        }
        finally
        {
            if (!handedOff)
            {
                FinalizeFetchFailure(entry);
                MarkProcessed(url);
                CompleteUrl();
            }
        }
    }

    private async Task RunParseWorker(CancellationToken cancellationToken)
    {
        var reader = _parseChannel.Reader;

        while (await reader.WaitToReadAsync(cancellationToken))
        {
            while (reader.TryRead(out var item))
            {
                await ProcessPage(item.Url, item.Response);
            }
        }
    }

    private async Task ProcessPage(string url, TResponse response)
    {
        _logger.LogInformation("Processing url '{url}'", url);

        _state.Reports.TryGetValue(url, out var entry);

        var document = default(TDocument);
        var parsed = false;
        try
        {
            var startTimestamp = Stopwatch.GetTimestamp();
            document = await ParseResponse(response);
            parsed = true;

            var pageBase = new Uri(url);
            var pageData = await ExtractPageData(document);

            entry?.ParseDuration = Stopwatch.GetElapsedTime(startTimestamp);

            var resolvedUrl = UriHelper.GetAbsoluteUrl(pageBase, pageData.CanonicalHref) ?? url;

            await AnalyzeDocument(resolvedUrl, document);

            var robots = _options.RespectMetaRobots ? pageData.Robots : RobotsRules.All;
            if (robots.Index)
                Visited.Add(resolvedUrl);

            var discovered = robots.Follow ? DiscoverOutgoing(pageBase, pageData.LinkHrefs, resolvedUrl) : 0;

            if (entry is not null)
            {
                entry.CanonicalUrl = resolvedUrl;
                entry.Indexed = robots.Index;
                entry.Followed = robots.Follow;
                entry.LinkCount = discovered;
                entry.Outcome = CrawlOutcome.Success;
            }
        }
        catch (OperationCanceledException) when (CrawlCancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            if (entry is not null)
            {
                entry.Outcome = CrawlOutcome.Timeout;
                entry.Error = ex.Message;
            }
            _logger.LogWarning("Timeout on url '{url}': {message}", url, ex.Message);
        }
        catch (Exception ex)
        {
            if (entry is not null)
            {
                entry.Outcome = CrawlOutcome.ParseError;
                entry.Error = ex.Message;
            }
            _logger.LogError(ex, "Encountered error {message}", ex.Message);
        }
        finally
        {
            MarkProcessed(url);

            if (parsed)
                await DisposeDocument(document!);

            await DisposeResponse(response);

            CompleteUrl();
        }
    }

    /// <summary>
    /// Resolves and enqueues the outgoing links of a parsed page, returning how many were newly queued.
    /// </summary>
    private int DiscoverOutgoing(Uri pageBase, IReadOnlyList<string?> hrefs, string resolvedUrl)
    {
        var discovered = 0;
        foreach (var href in hrefs)
        {
            var link = ResolveCrawlableUrl(pageBase, href);
            if (link == null)
                continue;

            if (Enqueue(link))
                discovered++;
        }

        if (discovered > 0)
            _logger.LogDebug("Found {count} new outgoing links on '{url}'", discovered, resolvedUrl);

        return discovered;
    }

    /// <summary>
    /// Called by fetch backends once the HTTP status is known, to enrich the per-URL report with the
    /// facts only the backend can see. Safe to call for any URL currently being fetched.
    /// </summary>
    protected void ReportResponse(string url, int statusCode, long? contentLength, string? contentType)
    {
        if (!_state.Reports.TryGetValue(url, out var entry))
            return;

        entry.StatusCode = statusCode;
        entry.ContentLength = contentLength;
        entry.ContentType = contentType;
    }

    /// <summary>
    /// Runs only on the fetch-failure path (no response handed to parsing), so a still-default Success
    /// outcome means no catch classified it: derive the failure from whatever status the backend reported.
    /// </summary>
    private static void FinalizeFetchFailure(UrlReport entry)
    {
        if (entry.Outcome != CrawlOutcome.Success)
            return;

        if (entry.StatusCode is int status)
            entry.Outcome = status.IsSuccessStatus() ? CrawlOutcome.FetchError : CrawlOutcome.HttpError;
        else
            entry.Outcome = CrawlOutcome.RetriesExhausted;
    }

    private bool Enqueue(string url)
    {
        if (!_state.Discovered.Add(url))
            return false;

        return EnqueueKnownUrl(url);
    }

    /// <summary>
    /// Schedules a URL already recorded in Discovered (e.g. restored from a checkpoint), bypassing the
    /// discovered-set dedupe that Enqueue performs.
    /// </summary>
    private bool EnqueueKnownUrl(string url)
    {
        if (!IsCrawlAllowed(url))
            return false;

        Interlocked.Increment(ref _outstanding);

        if (_urlChannel.Writer.TryWrite(url))
            return true;

        Interlocked.Decrement(ref _outstanding);
        return false;
    }

    protected virtual void DiscoverLink(Uri pageBase, string? href)
    {
        var url = ResolveCrawlableUrl(pageBase, href);
        if (url == null)
            return;

        Enqueue(url);
    }

    protected virtual string? ResolveCrawlableUrl(Uri pageBase, string? href)
    {
        if (InvalidateHref(href))
            return null;

        var url = UriHelper.GetAbsoluteUrl(pageBase, href);
        if (url == null)
            return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            return null;

        if (!_scopeAuthorities.Contains(absolute.Authority))
            return null;

        return url;
    }

    protected virtual bool InvalidateHref([NotNullWhen(false)] string? href)
    {
        if (href == null)
            return true;

        foreach (var linkPrefix in Constants.FilterLinkPrefixes)
        {
            if (href.StartsWith(linkPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var hrefSpan = href.AsSpan();
        var fileExtension = Path.GetExtension(hrefSpan);
        if (!fileExtension.IsEmpty)
        {
            if (!Constants.AllowedFileTypes.Contains(fileExtension, CharComparer.InvariantCultureIgnoreCase))
                return true;
        }

        return false;
    }

    protected abstract ValueTask<TResult> GetResult(CancellationToken cancellationToken);

    protected virtual ValueTask BackgroundDiscovery(CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Override to collect data beyond links from the already-parsed document. Runs on the parse workers,
    /// so any shared state it touches must be thread-safe, and any exception it throws is logged by
    /// ProcessPage rather than propagated - it will not stop the crawl.
    /// </summary>
    protected virtual ValueTask AnalyzeDocument(string url, TDocument document)
    {
        return ValueTask.CompletedTask;
    }

    protected abstract Task<TResponse?> LoadResponse(string url, CancellationToken cancellationToken);

    protected abstract ValueTask<TDocument> ParseResponse(TResponse response);

    protected abstract ValueTask<PageExtract> ExtractPageData(TDocument document);

    protected virtual Task DisposeDocument(TDocument document)
    {
        return Task.CompletedTask;
    }

    protected virtual Task DisposeResponse(TResponse? response)
    {
        return Task.CompletedTask;
    }

    protected virtual bool IsCrawlAllowed(string url)
    {
        return true;
    }

    protected virtual double GetCrawlDelay(string authority)
    {
        return _options.CrawlDelay;
    }
}
