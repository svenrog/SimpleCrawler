using SimpleCrawler.Core;
using SimpleCrawler.Core.Browser;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using PlaywrightContext = Microsoft.Playwright.Playwright;

namespace SimpleCrawler.Playwright;

public sealed class PlaywrightBrowserSession : IAsyncDisposable
{
    private readonly HeadlessCrawlerOptions _options;
    private readonly string? _initScript;
    private readonly SemaphoreSlim _initLock;

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private bool _disposed;

    public PlaywrightBrowserSession(IOptions<HeadlessCrawlerOptions> options)
    {
        _options = options.Value;
        _initLock = new SemaphoreSlim(1, 1);

        if (_options.BrowserProfile.Impersonate)
            _initScript = BrowserHelper.BuildInitScript(_options.BrowserProfile);
    }

    public async Task<IPage> NewPageAsync()
    {
        var context = await EnsureContext();
        return await context.NewPageAsync();
    }

    private async Task<IBrowserContext> EnsureContext()
    {
        if (_context is not null)
            return _context;

        await _initLock.WaitAsync();
        try
        {
            _playwright ??= await PlaywrightContext.CreateAsync();
            _browser ??= await LaunchBrowser(_playwright);

            if (_context is null)
            {
                var context = await _browser.NewContextAsync(GetContextOptions());

                if (_initScript != null)
                    await context.AddInitScriptAsync(_initScript);

                if (_options.BlockNonEssentialResources)
                    await context.RouteAsync("**/*", BlockNonEssentialResource);

                _context = context;
            }

            return _context;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private Task<IBrowser> LaunchBrowser(IPlaywright playwright)
    {
        var impersonate = _options.BrowserProfile.Impersonate;
        List<string> args = [.. Constants.DefaultArgs];

        if (impersonate)
            args.AddRange(Constants.UserImpersonationArgs);

        return playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !impersonate,
            Args = args
        });
    }

    private BrowserNewContextOptions GetContextOptions()
    {
        var profile = _options.BrowserProfile;

        return new BrowserNewContextOptions
        {
            UserAgent = profile.UserAgent,
            Locale = profile.Locale,
            ExtraHTTPHeaders = profile.AdditionalHeaders,
        };
    }

    private static Task BlockNonEssentialResource(IRoute route)
    {
        return route.Request.ResourceType is "image" or "media" or "font" or "stylesheet"
            ? route.AbortAsync()
            : route.ContinueAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_context is not null)
            await _context.DisposeAsync().ConfigureAwait(false);

        if (_browser is not null)
            await _browser.DisposeAsync().ConfigureAwait(false);

        _playwright?.Dispose();
        _initLock.Dispose();
    }
}
