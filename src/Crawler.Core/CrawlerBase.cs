using Crawler.Core.Collections;
using Crawler.Core.Comparers;
using Crawler.Core.Extensions;
using Crawler.Core.Helpers;
using Crawler.Core.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Crawler.Core;

public abstract class CrawlerBase<TResult>
    where TResult : IScrapeResult
{
    private readonly HttpClient _client;
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

    protected CrawlerBase(HttpClient client, IOptions<CrawlerOptions> options, ILogger logger)
    {
        _client = client;
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

    public async Task<TResult> Scrape(CancellationToken cancellationToken = default)
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

    private async Task ProcessPage(string url, CancellationToken cancellationToken)
    {
        _pending.Add(url);
        _logger.LogInformation("Processing url '{url}'", url);

        var canonicalUrl = (string?)null;

        try
        {
            var response = await LoadDocument(_client, url, cancellationToken);
            if (response == null)
                return;

            canonicalUrl = GetCanonical(response);

            var resolvedUrl = canonicalUrl ?? url;

            await AnalyzeDocument(resolvedUrl, response);

            var robots = GetRobotsRules(response);
            if (robots.Index)
                Visited.Add(resolvedUrl);

            if (!robots.Follow)
                return;

            var links = DiscoverLinks(response);
            if (links.Count > 0)
                _logger.LogDebug("Found {count} outgoing links on '{url}'", links.Count, resolvedUrl);

            _discovery.EnqueueAll(links);
            _discovered.AddRange(links);
        }
        catch (HtmlWebException ex)
        {
            _logger.LogWarning("Error on url '{url}': {message}", url, ex.Message);
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
        }
    }

    protected virtual ValueTask AnalyzeDocument(string url, HtmlDocument response)
    {
        return ValueTask.CompletedTask;
    }

    protected async virtual Task<HtmlDocument?> LoadDocument(HttpClient client, string url, CancellationToken cancellationToken)
    {
        var response = await client.GetAsync(url, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogDebug("Response '{code}' from url '{url}'", response.StatusCode, url);

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var document = new HtmlDocument();

            document.Load(stream);

            return document;
        }
        else
        {
            _logger.LogWarning("Error {code} on url '{url}'", response.StatusCode, url);

            return null;
        }
    }

    protected virtual HtmlNodeCollection? CollectLinks(HtmlDocument document)
    {
        return document.DocumentNode.SelectNodes("//a");
    }

    protected virtual string? GetCanonical(HtmlDocument document)
    {
        var linkElement = document.DocumentNode.SelectSingleNode("//link[@rel='canonical']");
        return linkElement?.Attributes["href"]?.Value;
    }

    protected virtual RobotsRules GetRobotsRules(HtmlDocument document)
    {
        // TODO: Parse and respect robots.txt

        if (!_options.RespectMetaRobots)
            return RobotsRules.All;

        var metaElement = document.DocumentNode.SelectSingleNode("//meta[@name='robots']");
        var contentRuleValue = metaElement?.Attributes["content"]?.Value;

        return IndexingHelper.ParseMetaRobots(contentRuleValue);
    }

    private HashSet<string> DiscoverLinks(HtmlDocument document)
    {
        var anchorElements = CollectLinks(document);
        var urls = new HashSet<string>(anchorElements?.Count ?? 0, StringComparer.OrdinalIgnoreCase);

        foreach (var anchorElement in anchorElements ?? Enumerable.Empty<HtmlNode>())
        {
            var href = anchorElement.Attributes["href"]?.Value;

            if (InvalidateHref(href))
                continue;

            if (!Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out var uri))
                continue;

            if (!uri.IsAbsoluteUri)
                uri = new Uri(_siteUri, uri);

            var url = uri.ToString();

            if (!url.StartsWith(_siteAuthority, StringComparison.OrdinalIgnoreCase))
                continue;

            if (_discovered.Contains(url))
                continue;

            urls.Add(url);
        }

        return urls;
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
}
