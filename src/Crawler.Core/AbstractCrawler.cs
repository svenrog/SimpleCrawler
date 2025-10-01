using Crawler.Core.Collections;
using Crawler.Core.Comparers;
using Crawler.Core.Extensions;
using Crawler.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Crawler.Core;

public abstract class AbstractCrawler<TResponse, TElement, TResult>
    where TResult : IScrapeResult
{
    private readonly CrawlerOptions _options;
    private readonly ConcurrentHashSet<string> _pending;
    private readonly ConcurrentHashSet<string> _processed;
    private readonly ConcurrentHashSet<string> _discovered;
    private readonly ConcurrentQueue<string> _discovery;
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger _logger;

    private readonly Uri _siteUri;
    private readonly string _siteAuthority;
    private readonly TimeSpan _delay;

    protected readonly ConcurrentHashSet<string> Visited;

    protected AbstractCrawler(IOptions<CrawlerOptions> options, ILogger logger)
    {
        _options = options.Value;
        _logger = logger;

        _siteUri = new Uri(_options.Entry);
        _siteAuthority = _siteUri.GetLeftPart(UriPartial.Authority);

        _semaphore = new SemaphoreSlim(1, _options.Parallelism);
        _delay = TimeSpan.FromSeconds(_options.CrawlDelay);

        Visited = [];

        _processed = [];
        _discovery = [];
        _pending = [];
        _discovered = [];

        _discovery.Enqueue(_options.Entry);
        _discovered.Add(_options.Entry);
    }

    public virtual async Task<TResult> Scrape(CancellationToken cancellationToken = default)
    {
        while ((!_discovery.IsEmpty || !_pending.IsEmpty) && _processed.Count < _options.MaxPages)
        {
            await _semaphore.WaitAsync(cancellationToken);

            _discovery.TryDequeue(out var nextPage);

            if (nextPage != null)
                _ = ProcessPage(nextPage, cancellationToken);

            if (_delay > TimeSpan.Zero)
                await Task.Delay(_delay, cancellationToken);
        }

        return await GetResult(cancellationToken);
    }

    protected abstract ValueTask<TResult> GetResult(CancellationToken cancellationToken);



    protected virtual ValueTask AnalyzeDocument(string url, TResponse response)
    {
        return ValueTask.CompletedTask;
    }

    protected abstract Task<TResponse?> LoadResponse(string url, CancellationToken cancellationToken);

    protected virtual Task DisposeResponse(TResponse? response)
    {
        return Task.CompletedTask;
    }

    protected abstract ValueTask<IEnumerable<TElement>> CollectLinks(TResponse response);

    protected abstract ValueTask<string?> GetCanonical(TResponse response);

    protected abstract ValueTask<string?> GetAttribute(TElement element, string attributeName);

    protected abstract ValueTask<RobotsRules> GetRobotsRules(TResponse response);

    protected virtual string? GetAbsoluteUrl(string? href)
    {
        if (!Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out var uri))
            return null;

        if (!uri.IsAbsoluteUri)
            uri = new Uri(_siteUri, uri);

        return uri.ToString();
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
        _pending.Add(url);
        _logger.LogInformation("Processing url '{url}'", url);

        var canonicalUrl = (string?)null;
        TResponse? response = default;

        try
        {
            response = await LoadResponse(url, cancellationToken);
            if (response == null)
                return;

            canonicalUrl = await GetCanonical(response);

            var resolvedUrl = canonicalUrl ?? url;

            await AnalyzeDocument(resolvedUrl, response);

            var robots = _options.RespectMetaRobots ? RobotsRules.All : await GetRobotsRules(response);
            if (robots.Index)
                Visited.Add(resolvedUrl);

            if (!robots.Follow)
                return;

            var links = await DiscoverLinks(response);
            if (links.Count > 0)
                _logger.LogDebug("Found {count} outgoing links on '{url}'", links.Count, resolvedUrl);

            _discovery.EnqueueAll(links);
            _discovered.AddRange(links);
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
            _processed.Add(url);
            _pending.Remove(url);

            if (canonicalUrl != null)
                _processed.Add(canonicalUrl);

            var need = Math.Min(_options.Parallelism, _discovery.Count);
            var release = Math.Max(1, need - 1);

            _semaphore.Release(release);

            await DisposeResponse(response);
        }
    }

    private async ValueTask<HashSet<string>> DiscoverLinks(TResponse document)
    {
        var anchorElements = await CollectLinks(document);

        if (!anchorElements.TryGetNonEnumeratedCount(out int elementCount))
            elementCount = 0;

        var urls = new HashSet<string>(elementCount, StringComparer.OrdinalIgnoreCase);

        foreach (var anchorElement in anchorElements)
        {
            var href = await GetAttribute(anchorElement, "href");

            if (InvalidateHref(href))
                continue;

            var url = GetAbsoluteUrl(href);
            if (url == null)
                continue;

            if (!url.StartsWith(_siteAuthority, StringComparison.OrdinalIgnoreCase))
                continue;

            if (_discovered.Contains(url))
                continue;

            urls.Add(url);
        }

        return urls;
    }
}
