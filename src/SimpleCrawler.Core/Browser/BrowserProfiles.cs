namespace SimpleCrawler.Core.Browser;

public static class BrowserProfiles
{
    public static IBrowserProfile Default => new DefaultBrowserProfile();
    public static IBrowserProfile Chrome => new ChromeBrowserProfile();
}
