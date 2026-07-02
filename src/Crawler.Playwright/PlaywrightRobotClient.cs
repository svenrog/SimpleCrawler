using Crawler.Core.Robots;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Crawler.Playwright;

public sealed class PlaywrightRobotClient : AbstractBrowserRobotClient
{
    private readonly PlaywrightBrowserSession _session;
    private readonly ILogger<PlaywrightRobotClient> _logger;

    public PlaywrightRobotClient(PlaywrightBrowserSession session, ILogger<PlaywrightRobotClient> logger)
    {
        _session = session;
        _logger = logger;
    }

    protected override async Task<RobotResourceResponse> FetchAsync(string url, CancellationToken cancellationToken)
    {
        var page = await _session.NewPageAsync();

        try
        {
            var response = await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            if (response is null)
            {
                _logger.LogWarning("No response from '{url}'", url);
                return new RobotResourceResponse(0, null, null);
            }

            var body = await response.BodyAsync();
            response.Headers.TryGetValue("content-type", out var contentType);

            return new RobotResourceResponse(response.Status, body, ParseMediaType(contentType));
        }
        catch (PlaywrightException e)
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
