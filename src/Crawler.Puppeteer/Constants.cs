using PuppeteerSharp;

namespace Crawler.Puppeteer;

internal static class Constants
{
    public static readonly NavigationOptions DefaultNavigationOptions = new()
    {
        WaitUntil = [WaitUntilNavigation.Networkidle0]
    };
}
