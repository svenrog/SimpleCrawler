using SimpleCrawler.Core;
using SimpleCrawler.Core.Checkpoints;
using SimpleCrawler.Core.Collectors;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;

namespace SimpleCrawler.Puppeteer;

public abstract class PuppeteerCrawler<TResult> : AbstractHeadlessCrawler<IPage, TResult>
    where TResult : IScrapeResult
{
    private readonly PuppeteerBrowserSession _session;
    private readonly ILogger _logger;

    protected PuppeteerCrawler(IRobotClient robotClient, PuppeteerBrowserSession session, IOptions<HeadlessCrawlerOptions> options, ILogger logger, IProxyPool? pool = null, ICheckpointStore? checkpoint = null, IEnumerable<ICrawlCollector>? collectors = null) : base(robotClient, options, logger, pool, checkpoint, collectors)
    {
        _session = session;
        _logger = logger;
    }

    protected override Task<IPage> NewPageAsync(ProxyInfo? proxy)
    {
        return _session.NewPageAsync(proxy);
    }

    protected virtual NavigationOptions GetNavigationOptions()
    {
        return Constants.DefaultNavigationOptions;
    }

    protected override async Task<(int? Status, IReadOnlyDictionary<string, string>? Headers)> NavigateAsync(IPage page, string url, ProxyInfo? proxy, CancellationToken cancellationToken)
    {
        IResponse? response;
        try
        {
            response = await page.GoToAsync(url, GetNavigationOptions()).WaitAsync(cancellationToken);
        }
        catch (PuppeteerException e)
        {
            _logger.LogDebug("Navigation to '{url}' via '{proxy}' failed: {message}", url, ProxyLabel.Describe(proxy), e.Message);
            return (null, null);
        }

        if (response is null)
        {
            _logger.LogWarning("No response from '{url}' via '{proxy}'", url, ProxyLabel.Describe(proxy));
            return (null, null);
        }

        // response.Headers comes straight from CDP, which already joins repeated headers (e.g. multiple
        // Set-Cookie) with a newline — the same ResponseSignal.HeaderValueSeparator the other backends use.
        return ((int)response.Status, CaptureSignals ? response.Headers : null);
    }

    protected override async Task ClosePageCore(IPage page)
    {
        try
        {
            await page.CloseAsync();
        }
        catch (PuppeteerException)
        {
        }
    }

    protected override async Task<string?> EvaluateExtractorAsync(IPage page, string script, CancellationToken cancellationToken)
    {
        return await page.EvaluateFunctionAsync<string?>(script, CaptureSignals).WaitAsync(cancellationToken);
    }
}
