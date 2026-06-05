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

public abstract class AbstractCrawler<TResponse, TResult>
    where TResult : IScrapeResult
{
    private readonly CrawlerOptions _options;
    private readonly ConcurrentHashSet<string> _discovered;
    private readonly SemaphoreSlim _throttleGate;
    private readonly ILogger _logger;

    private Channel<string> _channel;
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
        _channel = CreateChannel();
    }

    public virtual async Task<TResult> Start(string entry, CancellationToken cancellationToken = default)
    {
        await InitializeCrawl(entry, cancellationToken);

        var workers = new Task[WorkerCount];
        for (var i = 0; i < workers.Length; i++)
            workers[i] = RunWorker(cancellationToken);

        await Task.WhenAll(workers);

        return await GetResult(cancellationToken);
    }

    protected virtual int WorkerCount => Math.Max(1, _options.Parallelism);

    protected virtual ValueTask InitializeCrawl(string entry, CancellationToken cancellationToken)
    {
        var entryUri = new Uri(entry);

        _siteAuthority = entryUri.GetLeftPart(UriPartial.Authority);
        _siteUri = new Uri(_siteAuthority);

        Visited.Clear();
        _discovered.Clear();

        _channel = CreateChannel();
        _outstanding = 0;
        _processedCount = 0;
        _nextSlotTimestamp = 0;

        Enqueue(entry);

        return ValueTask.CompletedTask;
    }

    private static Channel<string> CreateChannel()
    {
        var options = new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
        };
        return Channel.CreateUnbounded<string>(options);
    }

    private async Task RunWorker(CancellationToken cancellationToken)
    {
        var reader = _channel.Reader;

        while (await reader.WaitToReadAsync(cancellationToken))
        {
            while (reader.TryRead(out var url))
            {
                try
                {
                    if (Volatile.Read(ref _processedCount) < _options.MaxPages)
                    {
                        await Throttle(cancellationToken);
                        await ProcessPage(url, cancellationToken);
                    }
                }
                finally
                {
                    if (Interlocked.Decrement(ref _outstanding) == 0)
                        _channel.Writer.TryComplete();
                }
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

    private void Enqueue(string url)
    {
        if (!_discovered.Add(url))
            return;

        Interlocked.Increment(ref _outstanding);

        if (!_channel.Writer.TryWrite(url))
            Interlocked.Decrement(ref _outstanding);
    }

    protected abstract ValueTask<TResult> GetResult(CancellationToken cancellationToken);

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

    private async Task ProcessPage(string url, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing url '{url}'", url);

        var canonicalUrl = (string?)null;
        TResponse? response = default;

        try
        {
            response = await LoadResponse(url, cancellationToken);
            if (response == null)
                return;

            var pageData = await ExtractPageData(response);

            canonicalUrl = pageData.CanonicalUrl;

            var resolvedUrl = canonicalUrl ?? url;

            await AnalyzeDocument(resolvedUrl, response);

            var robots = _options.RespectMetaRobots ? pageData.Robots : RobotsRules.All;
            if (robots.Index)
                Visited.Add(resolvedUrl);

            if (!robots.Follow)
                return;

            var links = FilterHrefs(pageData.LinkHrefs);
            if (links.Count > 0)
                _logger.LogDebug("Found {count} outgoing links on '{url}'", links.Count, resolvedUrl);

            foreach (var link in links)
                Enqueue(link);
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
        }
    }

    private HashSet<string> FilterHrefs(IReadOnlyList<string?> hrefs)
    {
        var urls = new HashSet<string>(hrefs.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var href in hrefs)
        {
            var url = GetUndiscoveredUrl(href);
            if (url == null)
                continue;

            urls.Add(url);
        }

        return urls;
    }

    protected virtual void DiscoverLink(string href)
    {
        var url = GetUndiscoveredUrl(href);
        if (url == null)
            return;

        Enqueue(url);
    }

    protected virtual string? GetUndiscoveredUrl(string? href)
    {
        if (InvalidateHref(href))
            return null;

        var url = GetAbsoluteUrl(href);
        if (url == null)
            return null;

        if (!url.StartsWith(_siteAuthority!, StringComparison.OrdinalIgnoreCase))
            return null;

        if (_discovered.Contains(url))
            return null;

        return url;
    }
}
