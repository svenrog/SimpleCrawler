using SimpleCrawler.Core.Browser;
using SystemConsole = System.Console;

namespace SimpleCrawler.Console.Helpers;

public static class ProfileMapper
{
    public static IBrowserProfile Map(Options options)
    {
        var profile = GetProfile(options);
        var extraHeaders = ParseHeaders(options.Headers);

        if (!string.IsNullOrEmpty(options.Cookie))
        {
            extraHeaders["Cookie"] = options.Cookie;
        }

        foreach (var header in extraHeaders)
        {
            if (profile.AdditionalHeaders.ContainsKey(header.Key))
            {
                profile.AdditionalHeaders[header.Key] = header.Value;
            }
            else
            {
                profile.AdditionalHeaders.Add(header.Key, header.Value);
            }
        }

        return profile;
    }

    private static IBrowserProfile GetProfile(Options options)
    {
        if (options.Impersonate == BrowserImpersonation.Chrome)
            return BrowserProfiles.Chrome;

        if (!string.IsNullOrEmpty(options.UserAgent))
            return new DefaultBrowserProfile { UserAgent = options.UserAgent };

        return BrowserProfiles.Default;
    }

    private static Dictionary<string, string> ParseHeaders(IEnumerable<string> headers)
    {
        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            var separator = header.IndexOf(':');
            if (separator <= 0)
            {
                SystemConsole.WriteLine($"Ignoring malformed header '{header}'; expected 'Name: Value'.");
                continue;
            }

            var name = header[..separator].Trim();
            var value = header[(separator + 1)..].Trim();

            if (name.Length == 0)
            {
                SystemConsole.WriteLine($"Ignoring malformed header '{header}'; expected 'Name: Value'.");
                continue;
            }

            parsed[name] = value;
        }

        return parsed;
    }
}
