using SimpleCrawler.Core;
using SimpleCrawler.Core.Extensions;
using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;

namespace SimpleCrawler.Puppeteer;

public sealed class PuppeteerRobotClient : AbstractBrowserRobotClient
{
    private readonly PuppeteerBrowserSession _session;
    private readonly ProxyRetryExecutor? _retry;
    private readonly ILogger<PuppeteerRobotClient> _logger;

    public PuppeteerRobotClient(PuppeteerBrowserSession session, IOptions<HeadlessCrawlerOptions> options, ILogger<PuppeteerRobotClient> logger, IProxyPool? pool = null)
    {
        _session = session;
        _retry = options.Value.ProxyPool is not null && pool is not null
            ? new ProxyRetryExecutor(pool, options.Value.ProxyPool.MaxRetries)
            : null;
        _logger = logger;
    }

    protected override async Task<RobotResourceResponse> FetchAsync(string url, CancellationToken cancellationToken)
    {
        if (_retry is null)
            return (await FetchClassified(url, null, cancellationToken)).Response;

        return await _retry.ExecuteWithDirectFallbackAsync(
            async (proxy, token) =>
            {
                var (response, kind) = await FetchClassified(url, proxy, token);
                return kind is null
                    ? ProxyAttempt<RobotResourceResponse>.Ok(response)
                    : ProxyAttempt<RobotResourceResponse>.Failed(kind.Value, response);
            },
            () => new RobotResourceResponse(0, null, null),
            cancellationToken);
    }

    private async Task<(RobotResourceResponse Response, ProxyFailureKind? Kind)> FetchClassified(string url, ProxyInfo? proxy, CancellationToken cancellationToken)
    {
        var page = await _session.NewPageAsync(proxy);

        try
        {
            var response = await page.GoToAsync(url, Constants.DefaultNavigationOptions).WaitAsync(cancellationToken);
            if (response is null)
            {
                _logger.LogDebug("No response from '{url}'", url);
                return (new RobotResourceResponse(0, null, null), ProxyFailureKind.Connection);
            }

            var status = (int)response.Status;
            var kind = ProxyFailureClassifier.Classify(status);

            // Only a successful response carries a body worth reading; mirroring the HttpClient robot
            // client, a non-success probe (e.g. an absent /sitemap.xml) is not a fetch error, and
            // reading its body would throw ("Unable to get response body").
            if (!status.IsSuccessStatus())
                return (new RobotResourceResponse(status, null, null), kind);

            var body = await response.BufferAsync().AsTask().WaitAsync(cancellationToken);
            response.Headers.TryGetValue("content-type", out var contentType);

            return (new RobotResourceResponse(status, body, ParseMediaType(contentType)), kind);
        }
        catch (PuppeteerException e)
        {
            _logger.LogDebug("Failed to fetch '{url}': {message}", url, e.Message);
            return (new RobotResourceResponse(0, null, null), ProxyFailureKind.Connection);
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
