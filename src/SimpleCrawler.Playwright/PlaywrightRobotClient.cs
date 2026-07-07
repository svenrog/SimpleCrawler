using SimpleCrawler.Core;
using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace SimpleCrawler.Playwright;

public sealed class PlaywrightRobotClient : AbstractBrowserRobotClient
{
    private readonly PlaywrightBrowserSession _session;
    private readonly ProxyRetryExecutor? _retry;
    private readonly ILogger<PlaywrightRobotClient> _logger;

    public PlaywrightRobotClient(PlaywrightBrowserSession session, IOptions<HeadlessCrawlerOptions> options, ILogger<PlaywrightRobotClient> logger, IProxyPool? pool = null)
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
            var response = await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }).WaitAsync(cancellationToken);
            if (response is null)
            {
                _logger.LogDebug("No response from '{url}'", url);
                return (new RobotResourceResponse(0, null, null), ProxyFailureKind.Connection);
            }

            var status = response.Status;
            var kind = ProxyFailureClassifier.Classify(status);

            // Only a successful response carries a body worth reading; mirroring the HttpClient robot
            // client, a non-success probe (e.g. an absent /sitemap.xml) is not a fetch error, and
            // reading its body would throw.
            if (status is < 200 or >= 300)
                return (new RobotResourceResponse(status, null, null), kind);

            var body = await response.BodyAsync().WaitAsync(cancellationToken);
            response.Headers.TryGetValue("content-type", out var contentType);

            return (new RobotResourceResponse(status, body, ParseMediaType(contentType)), kind);
        }
        catch (PlaywrightException e)
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
