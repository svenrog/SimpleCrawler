using Crawler.Core;
using Crawler.Core.Helpers;
using Crawler.Core.Models;
using Crawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using System.Collections.Concurrent;
using System.Text.Json;
using PuppeteerController = PuppeteerSharp.Puppeteer;

namespace Crawler.Puppeteer;

public abstract class PuppeteerCrawler<TResult> : AbstractRobotsCrawler<IPage, IElementHandle, TResult>, IAsyncDisposable
    where TResult : IScrapeResult
{
    private readonly CrawlerOptions _options;
    private readonly ILogger _logger;

    private IBrowser? _browser;
    private IBrowserContext? _browserContext;

    private readonly ConcurrentQueue<IPage> _pagePool;

    private bool _disposed;

    protected PuppeteerCrawler(IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger logger) : base(robotClient, options, logger)
    {
        _options = options.Value;
        _logger = logger;
        _pagePool = new ConcurrentQueue<IPage>();
    }

    public override async Task<TResult> Start(string entry, CancellationToken cancellationToken = default)
    {
        var fetcher = PuppeteerController.CreateBrowserFetcher(GetBrowserFetcherOptions());
        await fetcher.DownloadAsync();

        _browser ??= await PuppeteerController.LaunchAsync(GetLaunchOptions());
        _browserContext ??= await _browser.CreateBrowserContextAsync(GetBrowserContextOptions());

        return await base.Start(entry, cancellationToken);
    }

    protected override async Task<IPage?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        var page = await AcquirePage();

        var response = await page.GoToAsync(url, GetNavigationOptions());
        if (response == null)
        {
            _logger.LogWarning("No response from '{url}'", url);
            await DisposeResponse(page);

            return null;
        }
        else if ((int)response.Status < 300)
        {
            _logger.LogDebug("Response '{code}' from url '{url}'", response.Status, url);
            return page;
        }
        else
        {
            _logger.LogWarning("Error {code} on url '{url}'", response.Status, url);
            await DisposeResponse(page);

            return null;
        }
    }

    private async ValueTask<IPage> AcquirePage()
    {
        if (_pagePool.TryDequeue(out var page))
            return page;

        page = await _browserContext!.NewPageAsync();
        await ConfigurePage(page);

        return page;
    }

    protected virtual NavigationOptions GetNavigationOptions()
    {
        return Constants.DefaultNavigationOptions;
    }

    protected virtual async ValueTask ConfigurePage(IPage page)
    {
        if (_options.UserAgent != null)
        {
            await page.SetUserAgentAsync(_options.UserAgent);
        }

        if (_options.BlockNonEssentialResources)
        {
            await page.SetRequestInterceptionAsync(true);
            page.Request += BlockNonEssentialResource;
        }
    }

    private static async void BlockNonEssentialResource(object? sender, RequestEventArgs e)
    {
        try
        {
            var type = e.Request.ResourceType;
            if (type is ResourceType.Image or ResourceType.Media or ResourceType.Font or ResourceType.StyleSheet)
                await e.Request.AbortAsync();
            else
                await e.Request.ContinueAsync();
        }
        catch
        {
            // The request may already be handled by the time this fires; ignore the race.
        }
    }

    protected override async ValueTask<PageExtract> ExtractPageData(IPage response)
    {
        var json = await response.EvaluateFunctionAsync<JsonElement>(RenderedPageExtractor.Script);
        var (canonicalHref, robotsContent, linkHrefs) = RenderedPageExtractor.Parse(json);

        return new PageExtract(GetAbsoluteUrl(canonicalHref), IndexingHelper.ParseMetaRobots(robotsContent), linkHrefs);
    }

    protected override async ValueTask<IEnumerable<IElementHandle>> CollectLinks(IPage response)
    {
        return await response.QuerySelectorAllAsync("a");
    }

    protected override async ValueTask<string?> GetCanonical(IPage response)
    {
        var linkElement = await response.QuerySelectorAsync("link[rel='canonical']");
        if (linkElement == null)
            return null;

        var href = await GetAttribute(linkElement, "href");
        return GetAbsoluteUrl(href);
    }

    protected override async ValueTask<string?> GetAttribute(IElementHandle element, string attributeName)
    {
        var property = await element.GetPropertyAsync(attributeName);
        if (property == null)
            return null;

        var propertyValue = await property.JsonValueAsync();
        if (propertyValue == null)
            return null;

        return propertyValue.ToString();
    }

    protected override async ValueTask<RobotsRules> GetRobotsRules(IPage response)
    {
        var metaElement = await response.QuerySelectorAsync("meta[name='robots']");
        if (metaElement == null)
            return IndexingHelper.ParseMetaRobots(null);

        var contentRuleValue = await GetAttribute(metaElement, "content");
        return IndexingHelper.ParseMetaRobots(contentRuleValue);
    }

    protected virtual BrowserFetcherOptions GetBrowserFetcherOptions()
    {
        return new BrowserFetcherOptions();
    }

    protected virtual BrowserContextOptions GetBrowserContextOptions()
    {
        return new BrowserContextOptions();
    }

    protected virtual LaunchOptions GetLaunchOptions()
    {
        return new LaunchOptions
        {
            Headless = true
        };
    }

    protected override Task DisposeResponse(IPage? response)
    {
        if (response != null)
            _pagePool.Enqueue(response);

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (!_disposed)
        {
            while (_pagePool.TryDequeue(out var page))
                await page.DisposeAsync().ConfigureAwait(false);

            if (_browserContext is not null)
            {
                await _browserContext.CloseAsync().ConfigureAwait(false);
                _browserContext = null;
            }

            if (_browser is not null)
            {
                await _browser.DisposeAsync().ConfigureAwait(false);
                _browser = null;
            }

            _disposed = true;
        }
    }
}
