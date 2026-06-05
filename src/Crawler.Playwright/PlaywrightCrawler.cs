using Crawler.Core;
using Crawler.Core.Helpers;
using Crawler.Core.Models;
using Crawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using System.Collections.Concurrent;
using PlaywrightContext = Microsoft.Playwright.Playwright;

namespace Crawler.Playwright;

public abstract class PlaywrightCrawler<TResult> : AbstractRobotsCrawler<IPage, IElementHandle, TResult>, IAsyncDisposable
    where TResult : IScrapeResult
{
    private readonly CrawlerOptions _options;
    private readonly ILogger _logger;

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _browserContext;

    private readonly ConcurrentQueue<IPage> _pagePool;

    private bool _disposed;

    protected PlaywrightCrawler(IRobotClient robotClient, IOptions<CrawlerOptions> options, ILogger logger) : base(robotClient, options, logger)
    {
        _options = options.Value;
        _logger = logger;
        _pagePool = new ConcurrentQueue<IPage>();
    }

    public override async Task<TResult> Start(string entry, CancellationToken cancellationToken = default)
    {
        _playwright ??= await PlaywrightContext.CreateAsync();
        _browser ??= await LaunchBrowser(_playwright);

        if (_browserContext is null)
        {
            _browserContext = await _browser.NewContextAsync(GetContextOptions());

            if (_options.BlockNonEssentialResources)
                await _browserContext.RouteAsync("**/*", BlockNonEssentialResource);
        }

        return await base.Start(entry, cancellationToken);
    }

    protected virtual Task<IBrowser> LaunchBrowser(IPlaywright playwright)
    {
        return playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Args =
            [
                "--disable-gpu",
                "--disable-dev-shm-usage",
                "--disable-extensions",
                "--disable-background-networking",
            ]
        });
    }

    private static Task BlockNonEssentialResource(IRoute route)
    {
        return route.Request.ResourceType is "image" or "media" or "font" or "stylesheet"
            ? route.AbortAsync()
            : route.ContinueAsync();
    }

    protected virtual BrowserNewContextOptions GetContextOptions()
    {
        return new BrowserNewContextOptions
        {
            UserAgent = _options.UserAgent
        };
    }

    protected override async Task<IPage?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        var page = await AcquirePage();
        var response = await page.GotoAsync(url);

        if (response == null)
        {
            _logger.LogWarning("No response from '{url}'", url);
            await DisposeResponse(page);

            return null;
        }
        else if (response.Status < 300)
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

    protected override async ValueTask<PageExtract> ExtractPageData(IPage response)
    {
        var json = await response.EvaluateAsync(RenderedPageExtractor.Script);
        var (canonicalHref, robotsContent, linkHrefs) = RenderedPageExtractor.Parse(json.GetValueOrDefault());

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

        var href = await linkElement.GetAttributeAsync("href");
        return GetAbsoluteUrl(href);
    }

    protected override async ValueTask<string?> GetAttribute(IElementHandle element, string attributeName)
    {
        return await element.GetAttributeAsync(attributeName);
    }

    protected override async ValueTask<RobotsRules> GetRobotsRules(IPage response)
    {
        var metaElement = await response.QuerySelectorAsync("meta[name='robots']");
        if (metaElement == null)
            return IndexingHelper.ParseMetaRobots(null);

        var contentRuleValue = await metaElement.GetAttributeAsync("content");
        return IndexingHelper.ParseMetaRobots(contentRuleValue);
    }

    private async ValueTask<IPage> AcquirePage()
    {
        if (_pagePool.TryDequeue(out var page))
            return page;

        return await _browserContext!.NewPageAsync();
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
                await page.CloseAsync().ConfigureAwait(false);

            if (_browserContext is not null)
            {
                await _browserContext.DisposeAsync().ConfigureAwait(false);
                _browserContext = null;
            }

            if (_browser is not null)
            {
                await _browser.DisposeAsync().ConfigureAwait(false);
                _browser = null;
            }

            _playwright?.Dispose();
            _playwright = null;
            _disposed = true;
        }
    }
}
