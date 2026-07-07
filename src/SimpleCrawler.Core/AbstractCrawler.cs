using SimpleCrawler.Core.Collections;
using SimpleCrawler.Core.Comparers;
using SimpleCrawler.Core.Extensions;
using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Proxy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace SimpleCrawler.Core;

public abstract class AbstractCrawler<TResponse, TDocument, TResult> : ICrawler<TResult>
    where TResult : IScrapeResult
{
    private readonly CrawlerOptions _options;
    private readonly ConcurrentHashSet<string> _discovered;
    private readonly ILogger _logger;

    private Channel<string> _urlChannel;
    private Channel<(string Url, TResponse Response)> _parseChannel;
    private int _outstanding;
    private int _processedCount;
    private int _aborted;

    private HashSet<string> _scopeAuthorities;
    private Dictionary<string, Uri> _entryByAuthority;
    private Dictionary<string, HostThrottle> _throttles;

    // Every crawlable host is an entry host (scope is the exact entry-authority set), so the per-host
    // scheme+host Uri needed to load robots.txt is always known here.
    protected IReadOnlyDictionary<string, Uri> EntryUris => _entryByAuthority;

    protected readonly ConcurrentHashSet<string> Visited;

    protected AbstractCrawler(IOptions<CrawlerOptions> options, ILogger logger)
    {
        _options = options.Value;
        _logger = logger;

        _scopeAuthorities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _entryByAuthority = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
        _throttles = new Dictionary<string, HostThrottle>(StringComparer.OrdinalIgnoreCase);

        Visited = [];
        _discovered = [];
        _urlChannel = CreateUrlChannel();
        _parseChannel = CreateParseChannel();
    }

    public virtual Task<TResult> Start(string entry, CancellationToken cancellationToken = default)
    {
        return Start([entry], cancellationToken);
    }

    public virtual async Task<TResult> Start(IReadOnlyList<string> entries, CancellationToken cancellationToken = default)
    {
        await InitializeCrawl(entries, cancellationToken);

        Interlocked.Increment(ref _outstanding);

        var fetchCount = _options.EffectiveConcurrency;
        var parseCount = _options.EffectiveParseConcurrency;

        var tasks = new Task[1 + fetchCount + parseCount];
        tasks[0] = Task.Run(async () =>
        {
            try
            {
                await BackgroundDiscovery(cancellationToken);
            }
            finally
            {
                CompleteUrl();
            }
        }, cancellationToken);

        var index = 1;
        for (var i = 0; i < fetchCount; i++)
            tasks[index++] = RunFetchWorker(cancellationToken);
        for (var i = 0; i < parseCount; i++)
            tasks[index++] = RunParseWorker(cancellationToken);

        await Task.WhenAll(tasks);

        return await GetResult(cancellationToken);
    }

    // Fully resets per-crawl state so a single crawler instance can be reused across Start calls.
    protected virtual ValueTask InitializeCrawl(IReadOnlyList<string> entries, CancellationToken cancellationToken)
    {
        SetSiteIdentities(entries);

        Visited.Clear();
        _discovered.Clear();

        _urlChannel = CreateUrlChannel();
        _parseChannel = CreateParseChannel();
        _outstanding = 0;
        _processedCount = 0;
        _aborted = 0;

        _throttles = new Dictionary<string, HostThrottle>(StringComparer.OrdinalIgnoreCase);
        foreach (var authority in _scopeAuthorities)
            _throttles[authority] = new HostThrottle();

        foreach (var entry in entries)
            Enqueue(entry);

        return ValueTask.CompletedTask;
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

    // A URL leaves the system when its fetch fails or its parse completes; when none remain in
    // flight, both stages are drained and safe to complete (parse workers only block on read; only
    // fetch workers block on the bounded parse-channel write, which a non-empty system always drains).
    private void CompleteUrl()
    {
        if (Interlocked.Decrement(ref _outstanding) == 0)
        {
            _urlChannel.Writer.TryComplete();
            _parseChannel.Writer.TryComplete();
        }
    }

    // Soft-abort: stop scheduling new fetches (completing the URL channel makes Enqueue a no-op and
    // drains fetch workers) while letting in-flight parses finish, so Start returns partial results.
    protected void Abort(string reason)
    {
        if (Interlocked.CompareExchange(ref _aborted, 1, 0) != 0)
            return;

        _logger.LogCritical("Aborting crawl: {reason}.", reason);
        _urlChannel.Writer.TryComplete();
    }

    private async Task RunFetchWorker(CancellationToken cancellationToken)
    {
        var reader = _urlChannel.Reader;

        while (await reader.WaitToReadAsync(cancellationToken))
        {
            while (reader.TryRead(out var url))
            {
                if (Volatile.Read(ref _processedCount) >= _options.MaxPages)
                {
                    CompleteUrl();
                    continue;
                }

                if (Volatile.Read(ref _aborted) == 1)
                {
                    CompleteUrl();
                    continue;
                }

                var handedOff = false;
                try
                {
                    await Throttle(new Uri(url).Authority, cancellationToken);

                    var response = await LoadResponse(url, cancellationToken);
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
                    Abort("proxy pool exhausted");
                }
                catch (TimeoutException ex)
                {
                    _logger.LogWarning("Timeout fetching '{url}': {message}", url, ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Encountered error fetching '{url}': {message}", url, ex.Message);
                }
                finally
                {
                    if (!handedOff)
                    {
                        Interlocked.Increment(ref _processedCount);
                        CompleteUrl();
                    }
                }
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

    // Throttles per host: each authority keeps its own slot timeline so one host's Crawl-delay never
    // stalls fetches to another. GetCrawlDelay is read live so robots.txt Crawl-delay (resolved after
    // construction) is honoured.
    protected virtual async Task Throttle(string authority, CancellationToken cancellationToken)
    {
        var delaySeconds = GetCrawlDelay(authority);
        if (delaySeconds <= 0)
            return;

        if (!_throttles.TryGetValue(authority, out var throttle))
            return;

        var delay = TimeSpan.FromSeconds(delaySeconds);

        await throttle.Gate.WaitAsync(cancellationToken);
        try
        {
            var now = Stopwatch.GetTimestamp();
            if (throttle.NextSlot > now)
            {
                var wait = TimeSpan.FromSeconds((double)(throttle.NextSlot - now) / Stopwatch.Frequency);
                await Task.Delay(wait, cancellationToken);
            }

            throttle.NextSlot = Stopwatch.GetTimestamp() + (long)(delay.TotalSeconds * Stopwatch.Frequency);
        }
        finally
        {
            throttle.Gate.Release();
        }
    }

    protected virtual double GetCrawlDelay(string authority)
    {
        return _options.CrawlDelay;
    }

    private bool Enqueue(string url)
    {
        if (!_discovered.Add(url))
            return false;

        if (!IsCrawlAllowed(url))
            return false;

        Interlocked.Increment(ref _outstanding);

        if (_urlChannel.Writer.TryWrite(url))
            return true;

        Interlocked.Decrement(ref _outstanding);
        return false;
    }

    protected abstract ValueTask<TResult> GetResult(CancellationToken cancellationToken);

    protected virtual ValueTask BackgroundDiscovery(CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    // Override to collect data beyond links from the already-parsed document. Runs on the parse workers,
    // so any shared state it touches must be thread-safe, and any exception it throws is logged by
    // ProcessPage rather than propagated - it will not stop the crawl.
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

    private async Task ProcessPage(string url, TResponse response)
    {
        _logger.LogInformation("Processing url '{url}'", url);

        var document = default(TDocument);
        var parsed = false;
        try
        {
            document = await ParseResponse(response);
            parsed = true;

            var pageBase = new Uri(url);
            var pageData = await ExtractPageData(document);

            var resolvedUrl = UriHelper.GetAbsoluteUrl(pageBase, pageData.CanonicalHref) ?? url;

            await AnalyzeDocument(resolvedUrl, document);

            var robots = _options.RespectMetaRobots ? pageData.Robots : RobotsRules.All;
            if (robots.Index)
                Visited.Add(resolvedUrl);

            if (!robots.Follow)
                return;

            var discovered = 0;
            foreach (var href in pageData.LinkHrefs)
            {
                var link = ResolveCrawlableUrl(pageBase, href);
                if (link == null)
                    continue;

                if (Enqueue(link))
                    discovered++;
            }

            if (discovered > 0)
                _logger.LogDebug("Found {count} new outgoing links on '{url}'", discovered, resolvedUrl);
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("Timeout on url '{url}': {message}", url, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encountered error {message}", ex.Message);
        }
        finally
        {
            Interlocked.Increment(ref _processedCount);

            if (parsed)
                await DisposeDocument(document!);

            await DisposeResponse(response);

            CompleteUrl();
        }
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

    private sealed class HostThrottle
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public long NextSlot;
    }
}
