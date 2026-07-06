namespace SimpleCrawler.Core.Proxy;

public enum ProxyFailureKind
{
    Connection,
    Timeout,
    ProxyAuth,
    Http429,
    Http5xx,
}
