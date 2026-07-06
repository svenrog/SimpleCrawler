using Crawler.Core.Helpers;

namespace Crawler.Core.Browser;

public static class BrowserHelper
{
    public static string BuildInitScript(IBrowserProfile profile)
    {
        var languages = profile.AcceptLanguage?.Split(',')
            .Select(preference => preference.Split(';').First());

        var languagesLiteral = languages is null ? "null" : JsonLiteral.StringArray(languages);

        return
             "Object.defineProperty(navigator, 'webdriver', { get: () => undefined });\n" +
            $"Object.defineProperty(navigator, 'languages', {{ get: () => {languagesLiteral} }});";
    }
}
