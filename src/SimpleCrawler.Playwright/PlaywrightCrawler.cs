using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using SimpleCrawler.Core;
using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Collectors;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Core.Robots;
using System.Text.Json;

namespace SimpleCrawler.Playwright;

public abstract class PlaywrightCrawler<TResult> : AbstractHeadlessCrawler<IPage, TResult>
    where TResult : IScrapeResult
{
    private readonly PlaywrightBrowserSession _session;
    private readonly float _networkIdleGraceMs;
    private readonly ILogger _logger;

    protected PlaywrightCrawler(IRobotClient robotClient, PlaywrightBrowserSession session, IOptions<HeadlessCrawlerOptions> options, ILogger logger, IProxyPool? pool = null, ICheckpointStore? checkpoint = null, IEnumerable<ICrawlCollector>? collectors = null) : base(robotClient, options, logger, pool, checkpoint, collectors)
    {
        _session = session;
        _networkIdleGraceMs = options.Value.NetworkIdleGraceMs;
        _logger = logger;
    }

    protected override Task<IPage> NewPageAsync(ProxyInfo? proxy)
    {
        return _session.NewPageAsync(proxy);
    }

    protected override async Task<(int? Status, IReadOnlyDictionary<string, string>? Headers)> NavigateAsync(IPage page, string url, ProxyInfo? proxy, CancellationToken cancellationToken)
    {
        IResponse? response;
        try
        {
            response = await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.Load }).WaitAsync(cancellationToken);
        }
        catch (PlaywrightException e)
        {
            _logger.LogDebug("Navigation to '{url}' via '{proxy}' failed: {message}", url, ProxyLabel.Describe(proxy), e.Message);
            return (null, null);
        }

        if (response is null)
        {
            _logger.LogWarning("No response from '{url}' via '{proxy}'", url, ProxyLabel.Describe(proxy));
            return (null, null);
        }

        var headers = CaptureSignals ? await CollectHeadersAsync(response) : null;
        return (response.Status, headers);
    }

    /// <summary>
    /// The synchronous <c>Headers</c> property mirrors what page JS sees and omits <c>Set-Cookie</c> (a
    /// forbidden response header per the Fetch spec); <c>HeadersArrayAsync</c> reads the underlying
    /// protocol response directly and keeps it.
    /// </summary>
    private static async Task<Dictionary<string, string>> CollectHeadersAsync(IResponse response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in await response.HeadersArrayAsync())
        {
            var key = header.Name.ToLowerInvariant();
            headers[key] = headers.TryGetValue(key, out var existing) ? existing + "\n" + header.Value : header.Value;
        }

        return headers;
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
        var json = await page.EvaluateAsync(script, CaptureSignals).WaitAsync(cancellationToken);
        return json.GetValueOrDefault();
    }
}
