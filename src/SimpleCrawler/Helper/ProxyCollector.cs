namespace SimpleCrawler.Helper;

public static class ProxyCollector
{
    public static string[] Collect(string? proxy)
    {
        if (string.IsNullOrWhiteSpace(proxy))
            return [];

        if (File.Exists(proxy))
        {
            return [.. File.ReadLines(proxy)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith('#'))];
        }

        var trimmed = proxy.Trim();
        if (LooksLikeProxy(trimmed))
            return [trimmed];

        throw new InvalidOperationException($"Could not parse a proxy from '{proxy}'");
    }

    private static bool LooksLikeProxy(string value)
        => value.Contains(':') || value.Contains("://");
}
