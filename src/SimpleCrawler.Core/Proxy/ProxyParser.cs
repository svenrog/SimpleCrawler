using System.Globalization;

namespace SimpleCrawler.Core.Proxy;

internal static class ProxyParser
{
    public static ProxyInfo Parse(string input)
    {
        var trimmed = input.AsSpan().Trim();
        if (trimmed.IsEmpty)
            return Unknown();

        try
        {
            if (trimmed.IndexOf("://") >= 0)
            {
                if (!Uri.TryCreate(trimmed.ToString(), UriKind.Absolute, out var uri))
                    return Unknown();

                string? user = null;
                string? pass = null;
                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    var colon = uri.UserInfo.IndexOf(':');
                    if (colon >= 0)
                    {
                        user = uri.UserInfo[..colon];
                        pass = uri.UserInfo[(colon + 1)..];
                    }
                    else
                    {
                        user = uri.UserInfo;
                    }
                }

                return new ProxyInfo
                {
                    Host = uri.Host,
                    Port = uri.Port,
                    Protocol = GetProtocol(uri.Scheme),
                    Username = user,
                    Password = pass,
                };
            }

            var at = trimmed.IndexOf('@');
            if (at >= 0)
            {
                var userInfo = trimmed[..at].ToString();
                var hostPort = trimmed[(at + 1)..].ToString();
                var (host, port, ok) = SplitHostPort(hostPort);
                if (!ok)
                    return Unknown();

                var (u, p) = SplitUserInfo(userInfo);
                return new ProxyInfo
                {
                    Host = host,
                    Port = port,
                    Protocol = ProxyProtocol.Http,
                    Username = u,
                    Password = p,
                };
            }

            var parts = trimmed.ToString().Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var port2))
                return new ProxyInfo { Host = parts[0], Port = port2, Protocol = ProxyProtocol.Http };

            if (parts.Length >= 4 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var port4))
                return new ProxyInfo
                {
                    Host = parts[0],
                    Port = port4,
                    Protocol = ProxyProtocol.Http,
                    Username = parts[2],
                    Password = string.Join(':', parts[3..]),
                };

            return Unknown();
        }
        catch
        {
            return Unknown();
        }
    }

    public static ProxyProtocol GetProtocol(string scheme)
        => scheme.ToLowerInvariant() switch
        {
            "http" => ProxyProtocol.Http,
            "https" => ProxyProtocol.Https,
            "socks4" or "socks4a" => ProxyProtocol.Socks4,
            "socks5" or "socks5h" => ProxyProtocol.Socks5,
            _ => ProxyProtocol.Unknown,
        };

    private static ProxyInfo Unknown() => new() { Protocol = ProxyProtocol.Unknown };

    private static (string Host, int Port, bool Ok) SplitHostPort(string hostPort)
    {
        var i = hostPort.LastIndexOf(':');
        if (i <= 0)
            return default;

        if (!int.TryParse(hostPort[(i + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var port))
            return default;

        return (hostPort[..i], port, true);
    }

    private static (string? User, string? Pass) SplitUserInfo(string userInfo)
    {
        if (string.IsNullOrEmpty(userInfo))
            return (null, null);

        var i = userInfo.IndexOf(':');
        return i < 0 ? (userInfo, null) : (userInfo[..i], userInfo[(i + 1)..]);
    }
}
