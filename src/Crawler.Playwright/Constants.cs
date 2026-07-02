namespace Crawler.Playwright;

internal static class Constants
{
    public static readonly List<string> DefaultArgs =
    [
        "--disable-gpu",
        "--disable-dev-shm-usage",
        "--disable-extensions",
        "--disable-background-networking"
    ];

    public static readonly List<string> UserImpersonationArgs =
    [
        "--disable-blink-features=AutomationControlled",
        "--headless=new"
    ];
}
