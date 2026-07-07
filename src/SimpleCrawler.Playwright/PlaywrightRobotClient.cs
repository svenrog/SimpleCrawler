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
    private readonly IProxyPool? _pool;
    private readonly int _maxRetries;
    private readonly ILogger<PlaywrightRobotClient> _logger;

    public PlaywrightRobotClient(PlaywrightBrowserSession session, IOptions<HeadlessCrawlerOptions> options, ILogger<PlaywrightRobotClient> logger, IProxyPool? pool = null)
    {
        _session = session;
        _pool = options.Value.ProxyPool is not null ? pool : null;
        _maxRetries = options.Value.ProxyPool?.MaxRetries ?? 0;
        _logger = logger;
    }

    protected override async Task<RobotResourceResponse> FetchAsync(string url, CancellationToken cancellationToken)
    {
        if (_pool is null)
            return (await FetchClassified(url, null, cancellationToken)).Response;

        for (var attempt = 0; attempt <= _maxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var proxy = _pool.Acquire();
            if (proxy is null)
                return (await FetchClassified(url, null, cancellationToken)).Response;

            var (response, kind) = await FetchClassified(url, proxy, cancellationToken);
            if (kind is null)
            {
                _pool.ReportSuccess(proxy);
                return response;
            }

            _pool.ReportFailure(proxy, kind.Value);
        }

        return new RobotResourceResponse(0, null, null);
    }

    private async Task<(RobotResourceResponse Response, ProxyFailureKind? Kind)> FetchClassified(string url, ProxyInfo? proxy, CancellationToken cancellationToken)
    {
        var page = await _session.NewPageAsync(proxy);

        try
        {
            var response = await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }).WaitAsync(cancellationToken);
            if (response is null)
            {
                _logger.LogWarning("No response from '{url}'", url);
                return (new RobotResourceResponse(0, null, null), ProxyFailureKind.Connection);
            }

            var kind = ProxyFailureClassifier.Classify(response.Status);
            var body = await response.BodyAsync().WaitAsync(cancellationToken);
            response.Headers.TryGetValue("content-type", out var contentType);

            return (new RobotResourceResponse(response.Status, body, ParseMediaType(contentType)), kind);
        }
        catch (PlaywrightException e)
        {
            _logger.LogWarning("Failed to fetch '{url}': {message}", url, e.Message);
            return (new RobotResourceResponse(0, null, null), ProxyFailureKind.Connection);
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
