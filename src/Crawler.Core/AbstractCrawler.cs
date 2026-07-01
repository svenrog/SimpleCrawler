using Crawler.Core.Collections;
using Crawler.Core.Comparers;
using Crawler.Core.Extensions;
using Crawler.Core.Helpers;
using Crawler.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace Crawler.Core;

public abstract class AbstractCrawler<TResponse, TResult> : ICrawler<TResult>
    where TResult : IScrapeResult
{
    private readonly CrawlerOptions _options;
    private readonly ConcurrentHashSet<string> _discovered;
    private readonly SemaphoreSlim _throttleGate;
    private readonly ILogger _logger;

    private Channel<string> _urlChannel;
    private Channel<(string Url, TResponse Response)> _parseChannel;
    private int _outstanding;
    private int _processedCount;
    private long _nextSlotTimestamp;

    private Uri? _siteUri;
    private string? _siteAuthority;

    private readonly TimeSpan _delay;

    protected readonly ConcurrentHashSet<string> Visited;

    protected AbstractCrawler(IOptions<CrawlerOptions> options, ILogger logger)
    {
        _options = options.Value;
        _logger = logger;

        _throttleGate = new SemaphoreSlim(1, 1);
        _delay = TimeSpan.FromSeconds(_options.CrawlDelay);

        Visited = [];
        _discovered = [];
        _urlChannel = CreateUrlChannel();
        _parseChannel = CreateParseChannel();
    }

    public virtual async Task<TResult> Start(string entry, CancellationToken cancellationToken = default)
    {
        await InitializeCrawl(entry, cancellationToken);

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

    protected virtual ValueTask InitializeCrawl(string entry, CancellationToken cancellationToken)
    {
        var entryUri = new Uri(entry);

        _siteAuthority = entryUri.GetLeftPart(UriPartial.Authority);
        _siteUri = new Uri(_siteAuthority);

        Visited.Clear();
        _discovered.Clear();

        _urlChannel = CreateUrlChannel();
        _parseChannel = CreateParseChannel();
        _outstanding = 0;
        _processedCount = 0;
        _nextSlotTimestamp = 0;

        Enqueue(entry);

        return ValueTask.CompletedTask;
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

                var handedOff = false;
                try
                {
                    await Throttle(cancellationToken);

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

    protected virtual async Task Throttle(CancellationToken cancellationToken)
    {
        if (_delay <= TimeSpan.Zero)
            return;

        await _throttleGate.WaitAsync(cancellationToken);
        try
        {
            var now = Stopwatch.GetTimestamp();
            if (_nextSlotTimestamp > now)
            {
                var wait = TimeSpan.FromSeconds((double)(_nextSlotTimestamp - now) / Stopwatch.Frequency);
                await Task.Delay(wait, cancellationToken);
            }

            _nextSlotTimestamp = Stopwatch.GetTimestamp() + (long)(_delay.TotalSeconds * Stopwatch.Frequency);
        }
        finally
        {
            _throttleGate.Release();
        }
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

    protected virtual ValueTask AnalyzeDocument(string url, TResponse response)
    {
        return ValueTask.CompletedTask;
    }

    protected abstract Task<TResponse?> LoadResponse(string url, CancellationToken cancellationToken);

    protected abstract ValueTask<PageExtract> ExtractPageData(TResponse response);

    protected virtual Task DisposeResponse(TResponse? response)
    {
        return Task.CompletedTask;
    }

    protected virtual string? GetAbsoluteUrl(string? href)
    {
        return UriHelper.GetAbsoluteUrl(_siteUri!, href);
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

        try
        {
            var pageData = await ExtractPageData(response);

            var resolvedUrl = pageData.CanonicalUrl ?? url;

            await AnalyzeDocument(resolvedUrl, response);

            var robots = _options.RespectMetaRobots ? pageData.Robots : RobotsRules.All;
            if (robots.Index)
                Visited.Add(resolvedUrl);

            if (!robots.Follow)
                return;

            var discovered = 0;
            foreach (var href in pageData.LinkHrefs)
            {
                var link = ResolveCrawlableUrl(href);
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

            await DisposeResponse(response);

            CompleteUrl();
        }
    }

    protected virtual void DiscoverLink(string href)
    {
        var url = ResolveCrawlableUrl(href);
        if (url == null)
            return;

        Enqueue(url);
    }

    protected virtual string? ResolveCrawlableUrl(string? href)
    {
        if (InvalidateHref(href))
            return null;

        if (IsExternalAbsoluteUrl(href))
            return null;

        var url = GetAbsoluteUrl(href);
        if (url == null)
            return null;

        if (!url.StartsWith(_siteAuthority!, StringComparison.OrdinalIgnoreCase))
            return null;

        return url;
    }

    private bool IsExternalAbsoluteUrl(string href)
    {
        if (!href.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !href.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !href.StartsWith(_siteAuthority!, StringComparison.OrdinalIgnoreCase);
    }
}
