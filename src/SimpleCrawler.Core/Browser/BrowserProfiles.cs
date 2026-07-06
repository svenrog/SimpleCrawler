namespace Crawler.Core.Browser;

public static class BrowserProfiles
{
    public static readonly IBrowserProfile Default = new DefaultBrowserProfile();
    public static readonly IBrowserProfile Chrome = new ChromeBrowserProfile();
}
