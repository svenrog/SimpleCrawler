using SimpleCrawler.Core;
using SimpleCrawler.Core.Browser;
using SimpleCrawler.Core.Proxy;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using System.Collections.Concurrent;
using PuppeteerController = PuppeteerSharp.Puppeteer;

namespace SimpleCrawler.Puppeteer;

public sealed class PuppeteerBrowserSession : IAsyncDisposable
{
    private readonly HeadlessCrawlerOptions _options;
    private readonly IProxyPool? _pool;
    private readonly string? _initScript;
    private readonly string[] _launchArgs;
    private readonly SemaphoreSlim _initLock;
    private readonly ConcurrentDictionary<string, IBrowserContext> _contexts;

    private IBrowser? _browser;
    private bool _disposed;

    public PuppeteerBrowserSession(IOptions<HeadlessCrawlerOptions> options, IProxyPool? pool = null)
    {
        _options = options.Value;
        _pool = _options.ProxyPool is not null ? pool : null;
        _initLock = new SemaphoreSlim(1, 1);
        _contexts = new ConcurrentDictionary<string, IBrowserContext>();

        if (_pool is not null)
            BrowserProxyHelper.EnsureAllSupported(_pool.Proxies);

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

    public Task<IPage> NewPageAsync() => NewPageAsync(null);

    public async Task<IPage> NewPageAsync(ProxyInfo? proxy)
    {
        var context = await EnsureContext(proxy);
        var page = await context.NewPageAsync();
        await ConfigurePage(page, proxy);

        return page;
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

            var context = await _browser.CreateBrowserContextAsync(GetContextOptions(proxy));
            _contexts[key] = context;

            return context;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static BrowserContextOptions? GetContextOptions(ProxyInfo? proxy)
    {
        if (proxy is null)
            return null;

        BrowserProxyHelper.EnsureSupported(proxy);
        return new BrowserContextOptions
        {
            ProxyServer = BrowserProxyHelper.ToServerArg(proxy),
        };
    }

    private async ValueTask ConfigurePage(IPage page, ProxyInfo? proxy)
    {
        await page.SetUserAgentAsync(_options.BrowserProfile.UserAgent);
        await page.SetExtraHttpHeadersAsync(_options.BrowserProfile.AdditionalHeaders);

        if (proxy is not null && proxy.HasCredentials)
            await page.AuthenticateAsync(new Credentials { Username = proxy.Username, Password = proxy.Password });

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

        foreach (var context in _contexts.Values)
            await context.CloseAsync().ConfigureAwait(false);

        _contexts.Clear();

        if (_browser is not null)
            await _browser.DisposeAsync().ConfigureAwait(false);

        _initLock.Dispose();
    }
}
