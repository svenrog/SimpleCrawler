using SimpleCrawler.Core;
using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;

namespace SimpleCrawler.Puppeteer;

public sealed class PuppeteerRobotClient : AbstractBrowserRobotClient
{
    private readonly PuppeteerBrowserSession _session;
    private readonly IProxyPool? _pool;
    private readonly int _maxRetries;
    private readonly ILogger<PuppeteerRobotClient> _logger;

    public PuppeteerRobotClient(PuppeteerBrowserSession session, IOptions<HeadlessCrawlerOptions> options, ILogger<PuppeteerRobotClient> logger, IProxyPool? pool = null)
    {
        _session = session;
        _pool = options.Value.ProxyPool is not null ? pool : null;
        _maxRetries = options.Value.ProxyPool?.MaxRetries ?? 0;
        _logger = logger;
    }

    protected override async Task<RobotResourceResponse> FetchAsync(string url, CancellationToken cancellationToken)
    {
        if (_pool is null)
            return (await FetchClassified(url, null)).Response;

        for (var attempt = 0; attempt <= _maxRetries; attempt++)
        {
            var proxy = _pool.Acquire();
            if (proxy is null)
                return (await FetchClassified(url, null)).Response;

            var (response, kind) = await FetchClassified(url, proxy);
            if (kind is null)
            {
                _pool.ReportSuccess(proxy);
                return response;
            }

            _pool.ReportFailure(proxy, kind.Value);
        }

        return new RobotResourceResponse(0, null, null);
    }

    private async Task<(RobotResourceResponse Response, ProxyFailureKind? Kind)> FetchClassified(string url, ProxyInfo? proxy)
    {
        var page = await _session.NewPageAsync(proxy);

        try
        {
            var response = await page.GoToAsync(url, Constants.DefaultNavigationOptions);
            if (response is null)
            {
                _logger.LogWarning("No response from '{url}'", url);
                return (new RobotResourceResponse(0, null, null), ProxyFailureKind.Connection);
            }

            var status = (int)response.Status;
            var kind = ProxyFailureClassifier.Classify(status);
            var body = await response.BufferAsync();
            response.Headers.TryGetValue("content-type", out var contentType);

            return (new RobotResourceResponse(status, body, ParseMediaType(contentType)), kind);
        }
        catch (PuppeteerException e)
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
