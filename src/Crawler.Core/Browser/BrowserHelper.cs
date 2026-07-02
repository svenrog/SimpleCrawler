using System.Text.Json;

namespace Crawler.Core.Browser;

public static class BrowserHelper
{
    public static string BuildInitScript(IBrowserProfile profile)
    {
        var languages = profile.AcceptLanguage?.Split(',')
            .Select(preference => preference.Split(';').First())
            .ToArray();

        return
             "Object.defineProperty(navigator, 'webdriver', { get: () => undefined });\n" +
            $"Object.defineProperty(navigator, 'languages', {{ get: () => {JsonSerializer.Serialize(languages)} }});";
    }
}
