using SimpleCrawler.Core;
using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using System.Collections.Concurrent;
using System.Text.Json;

namespace SimpleCrawler.Puppeteer;

public abstract class PuppeteerCrawler<TResult> : AbstractRobotsCrawler<IPage, IPage, TResult>
    where TResult : IScrapeResult
{
    private readonly PuppeteerBrowserSession _session;
    private readonly ConcurrentQueue<IPage> _pagePool;
    private readonly ILogger _logger;

    protected PuppeteerCrawler(IRobotClient robotClient, PuppeteerBrowserSession session, IOptions<HeadlessCrawlerOptions> options, ILogger logger) : base(robotClient, options, logger)
    {
        _session = session;
        _logger = logger;
        _pagePool = new ConcurrentQueue<IPage>();
    }

    protected override async Task<IPage?> LoadResponse(string url, CancellationToken cancellationToken)
    {
        var page = await AcquirePage();

        var response = await page.GoToAsync(url, GetNavigationOptions());
        if (response == null)
        {
            _logger.LogWarning("No response from '{url}'", url);
            await DisposeResponse(page);

            return null;
        }
        else if ((int)response.Status < 300)
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

    private async ValueTask<IPage> AcquirePage()
    {
        if (_pagePool.TryDequeue(out var page))
            return page;

        return await _session.NewPageAsync();
    }

    protected virtual NavigationOptions GetNavigationOptions()
    {
        return Constants.DefaultNavigationOptions;
    }

    protected override ValueTask<IPage> ParseResponse(IPage response)
    {
        return new ValueTask<IPage>(response);
    }

    protected override async ValueTask<PageExtract> ExtractPageData(IPage response)
    {
        var json = await response.EvaluateFunctionAsync<JsonElement>(RenderedPageExtractor.Script);
        var (canonicalHref, robotsContent, linkHrefs) = RenderedPageExtractor.Parse(json);

        return new PageExtract(canonicalHref, IndexingHelper.ParseMetaRobots(robotsContent), linkHrefs);
    }

    protected override Task DisposeResponse(IPage? response)
    {
        if (response != null)
            _pagePool.Enqueue(response);

        return Task.CompletedTask;
    }
}
