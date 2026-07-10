namespace SimpleCrawler.Core.Proxy;

public sealed record ProxyInfo
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }
    public ProxyProtocol Protocol { get; init; } = ProxyProtocol.Unknown;
    public string? Username { get; init; }
    public string? Password { get; init; }

    public bool HasCredentials => !string.IsNullOrEmpty(Username);

    private string Scheme => Protocol switch
    {
        ProxyProtocol.Http => "http",
        ProxyProtocol.Https => "https",
        ProxyProtocol.Socks4 => "socks4",
        ProxyProtocol.Socks5 => "socks5",
        _ => "http",
    };

    public Uri ToUri() => new($"{Scheme}://{Host}:{Port}");

    public override string ToString() => $"{Scheme}://{Host}:{Port}";
}
