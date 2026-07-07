using SimpleCrawler.Core;
using SimpleCrawler.Core.Browser;
using SimpleCrawler.Core.Proxy;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using System.Collections.Concurrent;
using PlaywrightContext = Microsoft.Playwright.Playwright;

namespace SimpleCrawler.Playwright;

public sealed class PlaywrightBrowserSession : IAsyncDisposable
{
    private readonly HeadlessCrawlerOptions _options;
    private readonly IProxyPool? _pool;
    private readonly string? _initScript;
    private readonly SemaphoreSlim _initLock;
    private readonly ConcurrentDictionary<string, IBrowserContext> _contexts;

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _disposed;

    public PlaywrightBrowserSession(IOptions<HeadlessCrawlerOptions> options, IProxyPool? pool = null)
    {
        _options = options.Value;
        _pool = _options.ProxyPool is not null ? pool : null;
        _initLock = new SemaphoreSlim(1, 1);
        _contexts = new ConcurrentDictionary<string, IBrowserContext>();

        if (_pool is not null)
            BrowserProxyHelper.EnsureAllSupported(_pool.Proxies);

        if (_options.BrowserProfile.Impersonate)
            _initScript = BrowserHelper.BuildInitScript(_options.BrowserProfile);
    }

    public Task<IPage> NewPageAsync() => NewPageAsync(null);

    public async Task<IPage> NewPageAsync(ProxyInfo? proxy)
    {
        var context = await EnsureContext(proxy);
        return await context.NewPageAsync();
    }

    private async Task<IBrowserContext> EnsureContext(ProxyInfo? proxy)
    {
        var key = BrowserProxyHelper.ContextKey(proxy);
        if (_contexts.TryGetValue(key, out var existing))
            return existing;

        await _initLock.WaitAsync();
        try
        {
            if (_contexts.TryGetValue(key, out existing))
                return existing;

            _playwright ??= await PlaywrightContext.CreateAsync();
            _browser ??= await LaunchBrowser(_playwright);

            var context = await _browser.NewContextAsync(GetContextOptions(proxy));

            if (_initScript != null)
                await context.AddInitScriptAsync(_initScript);

            if (_options.BlockNonEssentialResources)
                await context.RouteAsync("**/*", BlockNonEssentialResource);

            _contexts[key] = context;
            return context;
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
            Args = args,
            // Chromium needs a launch-level proxy for per-context proxies to take effect.
            Proxy = _pool is not null ? new Proxy { Server = "per-context" } : null,
        });
    }

    private BrowserNewContextOptions GetContextOptions(ProxyInfo? proxy)
    {
        var profile = _options.BrowserProfile;

        var contextOptions = new BrowserNewContextOptions
        {
            UserAgent = profile.UserAgent,
            Locale = profile.Locale,
            ExtraHTTPHeaders = profile.AdditionalHeaders,
        };

        if (proxy is not null)
        {
            BrowserProxyHelper.EnsureSupported(proxy);
            contextOptions.Proxy = new Proxy
            {
                Server = BrowserProxyHelper.ToServerArg(proxy),
                Username = proxy.Username,
                Password = proxy.Password,
            };
        }

        return contextOptions;
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

        foreach (var context in _contexts.Values)
            await context.DisposeAsync().ConfigureAwait(false);

        _contexts.Clear();

        if (_browser is not null)
            await _browser.DisposeAsync().ConfigureAwait(false);

        _playwright?.Dispose();
        _initLock.Dispose();
    }
}
