using Crawler.Core.Collections;
using Crawler.Core.Extensions;
using Crawler.Core.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Crawler.Core;

public abstract class CrawlerBase<TResult>
    where TResult : IScrapeResult
{
    private readonly HttpClient _client;
    private readonly CrawlerOptions _options;
    private readonly ConcurrentHashSet<string> _pending;
    private readonly ConcurrentHashSet<string> _discovered;
    private readonly ConcurrentQueue<string> _discovery;
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger _logger;

    private readonly Uri _siteUri;
    private readonly string _siteAuthority;

    protected readonly ConcurrentHashSet<string> Visited;

    protected CrawlerBase(HttpClient client, IOptions<CrawlerOptions> options, ILogger logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;

        _siteUri = new Uri(_options.Entry);
        _siteAuthority = _siteUri.GetLeftPart(UriPartial.Authority);

        _semaphore = new SemaphoreSlim(1, _options.Parallellism);

        Visited = [];
        _discovery = [];
        _pending = [];
        _discovered = [];

        _discovery.Enqueue(_options.Entry);
        _discovered.Add(_options.Entry);
    }

    public async Task<TResult> Scrape(CancellationToken cancellationToken = default)
    {
        while ((!_discovery.IsEmpty || !_pending.IsEmpty) && Visited.Count < _options.MaxPages)
        {
            await _semaphore.WaitAsync(cancellationToken);

            _discovery.TryDequeue(out var nextPage);

            if (nextPage != null)
                _ = ProcessPage(nextPage, cancellationToken);
        }

        return await GetResult(cancellationToken);
    }

    protected abstract ValueTask<TResult> GetResult(CancellationToken cancellationToken);

    private async Task ProcessPage(string url, CancellationToken cancellationToken)
    {
        _pending.Add(url);
        _logger.LogInformation("Processing url '{url}'", url);

        try
        {
            var response = await LoadDocument(_client, url, cancellationToken);
            if (response == null)
                return;

            await AnalyzeDocument(url, response);

            var links = DiscoverLinks(response);

            if (links.Count > 0)
                _logger.LogDebug("Found {count} outgoing links on '{url}'", links.Count, url);

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
            Visited.Add(url);
            _pending.Remove(url);

            var need = Math.Min(_options.Parallellism, _discovery.Count);
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

    private HashSet<string> DiscoverLinks(HtmlDocument document)
    {
        var linkElements = CollectLinks(document);
        var links = new HashSet<string>(linkElements?.Count ?? 0, StringComparer.OrdinalIgnoreCase);

        foreach (var linkElement in linkElements ?? Enumerable.Empty<HtmlNode>())
        {
            var linkValue = linkElement.Attributes["href"]?.Value;

            if (linkValue == null || linkValue.StartsWith('#'))
                continue;

            if (!Uri.TryCreate(linkValue, UriKind.RelativeOrAbsolute, out var linkUri))
                continue;

            if (!linkUri.IsAbsoluteUri)
                linkUri = new Uri(_siteUri, linkUri);

            var linkUrl = linkUri.ToString();

            if (!linkUrl.StartsWith(_siteAuthority, StringComparison.OrdinalIgnoreCase))
                continue;

            if (_discovered.Add(linkUrl))
                continue;

            links.Add(linkUrl);
        }

        return links;
    }
}
