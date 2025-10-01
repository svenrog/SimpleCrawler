using Crawler.Core;
using Crawler.Core.Helpers;
using Crawler.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using PlaywrightContext = Microsoft.Playwright.Playwright;

namespace Crawler.Playwright;

public abstract class PlaywrightCrawler<TResult> : AbstractCrawler<IPage, IElementHandle, TResult>, IAsyncDisposable
    where TResult : IScrapeResult
{
    private readonly ILogger _logger;

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _browserContext;

    private bool _disposed;

    protected PlaywrightCrawler(IOptions<CrawlerOptions> options, ILogger logger) : base(options, logger)
    {
        _logger = logger;
    }

    public override async Task<TResult> Scrape(CancellationToken cancellationToken = default)
    {
        _playwright ??= await PlaywrightContext.CreateAsync();
        _browser ??= await LaunchBrowser(_playwright);
        _browserContext ??= await _browser.NewContextAsync();

        return await base.Scrape(cancellationToken);
    }

    protected virtual Task<IBrowser> LaunchBrowser(IPlaywright playwright)
    {
        return playwright.Chromium.LaunchAsync();
    }

    protected override async Task<IPage?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        var page = await _browserContext!.NewPageAsync();
        var response = await page.GotoAsync(url);

        await page.WaitForLoadStateAsync(LoadState.Load);

        if (response == null)
        {
            _logger.LogWarning("No response from '{url}'", url);
            await page.CloseAsync();

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
            await page.CloseAsync();

            return null;
        }
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

    protected async override Task DisposeResponse(IPage? response)
    {
        if (response == null)
            return;

        await response.CloseAsync();
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
