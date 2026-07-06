using PuppeteerSharp;

namespace SimpleCrawler.Puppeteer;

internal static class Constants
{
    public static readonly NavigationOptions DefaultNavigationOptions = new()
    {
        WaitUntil = [WaitUntilNavigation.Networkidle0]
    };

    public static readonly string[] DefaultLaunchArgs = ["--no-sandbox", "--disable-setuid-sandbox"];

    public static readonly string[] UserImpersonationArgs = ["--disable-blink-features=AutomationControlled", "--headless=new"];
}
