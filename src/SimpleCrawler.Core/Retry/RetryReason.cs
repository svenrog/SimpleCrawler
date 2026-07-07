namespace SimpleCrawler.Core.Retry;

public enum RetryReason
{
    Connection,
    Timeout,
    RateLimited,
    ServerError,
    ProxyAuth,
}
