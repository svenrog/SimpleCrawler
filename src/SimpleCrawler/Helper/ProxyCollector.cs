namespace SimpleCrawler.Helper;

public static class ProxyCollector
{
    public static string[] Collect(string? proxy)
    {
        if (string.IsNullOrWhiteSpace(proxy))
            return [];

        try
        {
            string[] proxies = [.. File.ReadLines(proxy)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim())
                .Where(IsProxy)];

            if (proxies.Length > 0)
                return proxies;
        }
        catch
        {
            if (IsProxy(proxy))
                return [proxy];
        }

        throw new InvalidOperationException($"Could not parse a proxy from '{proxy}'");
    }

    private static bool IsProxy(string proxy)
    {
        if (Uri.TryCreate(proxy, UriKind.Absolute, out _))
            return true;

        return false;
    }
}
