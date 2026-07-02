using Crawler.Core;
using Crawler.Core.Helpers;
using Crawler.Core.Models;
using Crawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using System.Collections.Concurrent;

namespace Crawler.Playwright;

public abstract class PlaywrightCrawler<TResult> : AbstractRobotsCrawler<IPage, TResult>
    where TResult : IScrapeResult
{
    private readonly PlaywrightBrowserSession _session;
    private readonly HeadlessCrawlerOptions _options;
    private readonly ILogger _logger;

    private readonly ConcurrentQueue<IPage> _pagePool;

    protected PlaywrightCrawler(IRobotClient robotClient, PlaywrightBrowserSession session, IOptions<HeadlessCrawlerOptions> options, ILogger logger) : base(robotClient, options, logger)
    {
        _session = session;
        _options = options.Value;
        _logger = logger;
        _pagePool = new ConcurrentQueue<IPage>();
    }

    protected override async Task<IPage?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        var page = await AcquirePage();
        var response = await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

        if (response == null)
        {
            _logger.LogWarning("No response from '{url}'", url);
            await DisposeResponse(page);

            return null;
        }
        else if (response.Status >= 200 && response.Status < 300)
        {
            await WaitForNetworkIdle(page);
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

    private async Task WaitForNetworkIdle(IPage page)
    {
        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = _options.NetworkIdleGraceMs });
        }
        catch (System.TimeoutException)
        {
        }
    }

    private async ValueTask<IPage> AcquirePage()
    {
        if (_pagePool.TryDequeue(out var page))
            return page;

        return await _session.NewPageAsync();
    }

    protected override Task DisposeResponse(IPage? response)
    {
        if (response != null)
            _pagePool.Enqueue(response);

        return Task.CompletedTask;
    }
}
