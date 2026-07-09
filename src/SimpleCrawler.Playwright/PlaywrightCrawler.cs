using SimpleCrawler.Core;
using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using System.Text.Json;

namespace SimpleCrawler.Playwright;

public abstract class PlaywrightCrawler<TResult> : AbstractHeadlessCrawler<IPage, TResult>
    where TResult : IScrapeResult
{
    private readonly PlaywrightBrowserSession _session;
    private readonly float _networkIdleGraceMs;
    private readonly ILogger _logger;

    protected PlaywrightCrawler(IRobotClient robotClient, PlaywrightBrowserSession session, IOptions<HeadlessCrawlerOptions> options, ILogger logger, IProxyPool? pool = null, ICheckpointStore? checkpoint = null) : base(robotClient, options, logger, pool, checkpoint)
    {
        _session = session;
        _networkIdleGraceMs = options.Value.NetworkIdleGraceMs;
        _logger = logger;
    }

    protected override Task<IPage> NewPageAsync(ProxyInfo? proxy)
    {
        return _session.NewPageAsync(proxy);
    }

    protected override async Task<int?> NavigateAsync(IPage page, string url, ProxyInfo? proxy, CancellationToken cancellationToken)
    {
        IResponse? response;
        try
        {
            response = await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.Load }).WaitAsync(cancellationToken);
        }
        catch (PlaywrightException e)
        {
            _logger.LogDebug("Navigation to '{url}' via proxy {proxy} failed: {message}", url, proxy, e.Message);
            return null;
        }

        if (response is null)
        {
            _logger.LogWarning("No response from '{url}' via proxy {proxy}", url, proxy);
            return null;
        }

        return response.Status;
    }

    protected override async Task AfterSuccessfulLoad(IPage page, CancellationToken cancellationToken)
    {
        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = _networkIdleGraceMs }).WaitAsync(cancellationToken);
        }
        catch (System.TimeoutException)
        {
        }
    }

    protected override async Task ClosePageCore(IPage page)
    {
        try
        {
            await page.CloseAsync();
        }
        catch (PlaywrightException)
        {
        }
    }

    protected override async Task<JsonElement> EvaluateExtractorAsync(IPage page, string script, CancellationToken cancellationToken)
    {
        var json = await page.EvaluateAsync(script).WaitAsync(cancellationToken);
        return json.GetValueOrDefault();
    }
}
