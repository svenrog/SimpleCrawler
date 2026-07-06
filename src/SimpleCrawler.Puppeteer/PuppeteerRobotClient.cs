using SimpleCrawler.Core.Robots;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;

namespace SimpleCrawler.Puppeteer;

public sealed class PuppeteerRobotClient : AbstractBrowserRobotClient
{
    private readonly PuppeteerBrowserSession _session;
    private readonly ILogger<PuppeteerRobotClient> _logger;

    public PuppeteerRobotClient(PuppeteerBrowserSession session, ILogger<PuppeteerRobotClient> logger)
    {
        _session = session;
        _logger = logger;
    }

    protected override async Task<RobotResourceResponse> FetchAsync(string url, CancellationToken cancellationToken)
    {
        var page = await _session.NewPageAsync();

        try
        {
            var response = await page.GoToAsync(url, Constants.DefaultNavigationOptions);
            if (response is null)
            {
                _logger.LogWarning("No response from '{url}'", url);
                return new RobotResourceResponse(0, null, null);
            }

            var body = await response.BufferAsync();
            response.Headers.TryGetValue("content-type", out var contentType);

            return new RobotResourceResponse((int)response.Status, body, ParseMediaType(contentType));
        }
        catch (PuppeteerException e)
        {
            _logger.LogWarning("Failed to fetch '{url}': {message}", url, e.Message);
            return new RobotResourceResponse(0, null, null);
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
