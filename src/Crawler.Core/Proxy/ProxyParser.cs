namespace Crawler.Core.Proxy;

internal static class ProxyParser
{
    public static ProxyInfo Parse(string input)
    {
        if (input.Contains("://"))
        {
            var uri = new Uri(input);

            return new ProxyInfo
            {
                Host = uri.Host,
                Port = uri.Port,
                Protocol = GetProtocol(uri.Scheme)
            };
        }

        var parts = input.Split(':');

        return new ProxyInfo
        {
            Host = parts[0],
            Port = int.Parse(parts[1]),
            Protocol = ProxyProtocol.Unknown
        };
    }

    public static ProxyProtocol GetProtocol(string scheme)
    {
        return scheme.ToLower() switch
        {
            "http" => ProxyProtocol.Http,
            "https" => ProxyProtocol.Https,
            "socks4" => ProxyProtocol.Socks4,
            "socks5" => ProxyProtocol.Socks5,
            _ => ProxyProtocol.Unknown
        };
    }
}
