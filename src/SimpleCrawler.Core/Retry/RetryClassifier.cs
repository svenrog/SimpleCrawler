namespace SimpleCrawler.Core.Retry;

public static class RetryClassifier
{
    public static RetryReason? Classify(int statusCode)
    {
        if (statusCode == 407)
            return RetryReason.ProxyAuth;
        if (statusCode == 429)
            return RetryReason.RateLimited;
        if (statusCode is 500 or 502 or 503 or 504)
            return RetryReason.ServerError;
        return null;
    }

    public static RetryReason Classify(Exception exception)
    {
        if (exception is TimeoutException)
            return RetryReason.Timeout;
        return RetryReason.Connection;
    }
}
