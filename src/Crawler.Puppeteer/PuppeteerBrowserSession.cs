using Crawler.Core;
using Crawler.Core.Browser;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using PuppeteerController = PuppeteerSharp.Puppeteer;

namespace Crawler.Puppeteer;

public sealed class PuppeteerBrowserSession : IAsyncDisposable
{
    private readonly HeadlessCrawlerOptions _options;
    private readonly string? _initScript;
    private readonly string[] _launchArgs;
    private readonly SemaphoreSlim _initLock;

    private IBrowser? _browser;
    private IBrowserContext? _context;
    private bool _disposed;

    public PuppeteerBrowserSession(IOptions<HeadlessCrawlerOptions> options)
    {
        _options = options.Value;
        _initLock = new SemaphoreSlim(1, 1);

        if (_options.BrowserProfile.Impersonate)
        {
            _initScript = BrowserHelper.BuildInitScript(_options.BrowserProfile);
            _launchArgs = Constants.UserImpersonationArgs;
        }
        else
        {
            _launchArgs = Constants.DefaultLaunchArgs;
        }
    }

    public async Task<IPage> NewPageAsync()
    {
        var context = await EnsureContext();
        var page = await context.NewPageAsync();
        await ConfigurePage(page);

        return page;
    }

    private async Task<IBrowserContext> EnsureContext()
    {
        if (_context is not null)
            return _context;

        await _initLock.WaitAsync();
        try
        {
            if (_browser is null)
            {
                var fetcher = PuppeteerController.CreateBrowserFetcher(new BrowserFetcherOptions());
                await fetcher.DownloadAsync();

                _browser = await PuppeteerController.LaunchAsync(new LaunchOptions
                {
                    Headless = !_options.BrowserProfile.Impersonate,
                    Args = _launchArgs
                });
            }

            _context ??= await _browser.CreateBrowserContextAsync();

            return _context;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async ValueTask ConfigurePage(IPage page)
    {
        await page.SetUserAgentAsync(_options.BrowserProfile.UserAgent);
        await page.SetExtraHttpHeadersAsync(_options.BrowserProfile.AdditionalHeaders);

        if (_initScript != null)
            await page.EvaluateExpressionOnNewDocumentAsync(_initScript);

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

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_context is not null)
            await _context.CloseAsync().ConfigureAwait(false);

        if (_browser is not null)
            await _browser.DisposeAsync().ConfigureAwait(false);

        _initLock.Dispose();
    }
}
