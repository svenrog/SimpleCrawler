namespace Crawler.Core.Proxy;

public sealed record ProxyInfo
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }
    public ProxyProtocol Protocol { get; set; } = ProxyProtocol.Unknown;

    public override string ToString()
        => $"{Protocol.ToString().ToLower()}://{Host}:{Port}";

    public Uri ToUri()
        => new(ToString());
}
