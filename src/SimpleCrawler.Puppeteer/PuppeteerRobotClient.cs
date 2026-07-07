using SimpleCrawler.Core;
using SimpleCrawler.Core.Extensions;
using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Core.Retry;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;

namespace SimpleCrawler.Puppeteer;

public sealed class PuppeteerRobotClient : AbstractBrowserRobotClient
{
    private readonly PuppeteerBrowserSession _session;
    private readonly RetryExecutor _retry;
    private readonly ILogger<PuppeteerRobotClient> _logger;

    public PuppeteerRobotClient(PuppeteerBrowserSession session, IOptions<HeadlessCrawlerOptions> options, ILogger<PuppeteerRobotClient> logger, IProxyPool? pool = null)
    {
        _session = session;
        _retry = new RetryExecutor(options.Value.Retry, pool);
        _logger = logger;
    }

    protected override async Task<RobotResourceResponse> FetchAsync(string url, CancellationToken cancellationToken)
    {
        return await _retry.ExecuteWithDirectFallbackAsync(
            async (proxy, token) =>
            {
                var (response, reason) = await FetchClassified(url, proxy, token);
                return reason is null
                    ? RetryAttempt<RobotResourceResponse>.Ok(response)
                    : RetryAttempt<RobotResourceResponse>.Failed(reason.Value, response);
            },
            () => new RobotResourceResponse(0, null, null),
            cancellationToken);
    }

    private async Task<(RobotResourceResponse Response, RetryReason? Reason)> FetchClassified(string url, ProxyInfo? proxy, CancellationToken cancellationToken)
    {
        var page = await _session.NewPageAsync(proxy);

        try
        {
            var response = await page.GoToAsync(url, Constants.DefaultNavigationOptions).WaitAsync(cancellationToken);
            if (response is null)
            {
                _logger.LogDebug("No response from '{url}'", url);
                return (new RobotResourceResponse(0, null, null), RetryReason.Connection);
            }

            var status = (int)response.Status;
            var reason = RetryClassifier.Classify(status);

            // Only a successful response carries a body worth reading; mirroring the HttpClient robot
            // client, a non-success probe (e.g. an absent /sitemap.xml) is not a fetch error, and
            // reading its body would throw ("Unable to get response body").
            if (!status.IsSuccessStatus())
                return (new RobotResourceResponse(status, null, null), reason);

            var body = await response.BufferAsync().AsTask().WaitAsync(cancellationToken);
            response.Headers.TryGetValue("content-type", out var contentType);

            return (new RobotResourceResponse(status, body, ParseMediaType(contentType)), reason);
        }
        catch (PuppeteerException e)
        {
            _logger.LogDebug("Failed to fetch '{url}': {message}", url, e.Message);
            return (new RobotResourceResponse(0, null, null), RetryReason.Connection);
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
