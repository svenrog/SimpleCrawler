using Crawler.Core.Extensions;
using Crawler.Core.Models;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Crawler.Core;

public abstract class CrawlerBase
{
    private readonly HtmlWeb _client;
    private readonly CrawlerOptions _options;
    private readonly ConcurrentDictionary<string, bool> _visited;
    private readonly ConcurrentDictionary<string, bool> _pending;
    private readonly ConcurrentDictionary<string, bool> _discovered;
    private readonly ConcurrentQueue<string> _discovery;
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger _logger;

    private readonly Uri _siteUri;
    private readonly string _siteAuthority;

    protected CrawlerBase(HtmlWeb client, IOptions<CrawlerOptions> options, ILogger logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;

        _siteUri = new Uri(_options.Entry);
        _siteAuthority = _siteUri.GetLeftPart(UriPartial.Authority);

        _semaphore = new SemaphoreSlim(1, _options.Parallellism);

        _visited = [];
        _discovery = [];
        _pending = [];
        _discovered = [];

        _discovery.Enqueue(_options.Entry);
        _discovered.TryAdd(_options.Entry, true);
    }

    public async Task<IScrapeResult> Scrape(CancellationToken cancellationToken = default)
    {
        while ((!_discovery.IsEmpty || !_pending.IsEmpty) && _visited.Count < _options.MaxPages)
        {
            await _semaphore.WaitAsync(cancellationToken);

            _discovery.TryDequeue(out var nextPage);

            if (nextPage != null)
                _ = ProcessPage(nextPage, cancellationToken);
        }

        return await GetResult(cancellationToken);
    }

    protected abstract ValueTask<IScrapeResult> GetResult(CancellationToken cancellationToken);

    private async Task ProcessPage(string url, CancellationToken cancellationToken)
    {
        _pending.TryAdd(url, true);
        _logger.LogInformation("Processing url '{url}'", url);

        try
        {
            var response = await LoadUrl(_client, url, cancellationToken);
            _logger.LogDebug("Response from url '{url}'", url);

            await AnalyzeDocument(url, response);

            var links = DiscoverLinks(response);

            if (links.Count > 0)
                _logger.LogDebug("Found {count} outgoing links on '{url}'", links.Count, url);

            _discovery.EnqueueAll(links);
            _discovered.AddKeyRange(links, true);
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
            _visited.TryAdd(url, true);
            _pending.TryRemove(url, out bool _);

            var need = Math.Min(_options.Parallellism, _discovery.Count);
            var release = Math.Max(1, need - 1);

            _semaphore.Release(release);
        }
    }

    protected virtual ValueTask AnalyzeDocument(string url, HtmlDocument response)
    {
        return ValueTask.CompletedTask;
    }

    protected virtual Task<HtmlDocument> LoadUrl(HtmlWeb client, string url, CancellationToken cancellationToken)
    {
        return client.LoadFromWebAsync(url, cancellationToken);
    }

    protected virtual IList<HtmlNode> CollectLinks(HtmlDocument document)
    {
        return document.DocumentNode.QuerySelectorAll("a");
    }

    private HashSet<string> DiscoverLinks(HtmlDocument document)
    {
        var linkElements = CollectLinks(document);
        var links = new HashSet<string>(linkElements.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var linkElement in linkElements)
        {
            var linkValue = linkElement.Attributes["href"].Value;

            if (linkValue.StartsWith('#'))
                continue;

            if (!Uri.TryCreate(linkValue, UriKind.RelativeOrAbsolute, out var linkUri))
                continue;

            if (!linkUri.IsAbsoluteUri)
                linkUri = new Uri(_siteUri, linkUri);

            var linkUrl = linkUri.ToString();

            if (!linkUrl.StartsWith(_siteAuthority, StringComparison.OrdinalIgnoreCase))
                continue;

            if (_discovered.ContainsKey(linkUrl))
                continue;

            links.Add(linkUrl);
        }

        return links;
    }
}
