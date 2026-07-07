namespace SimpleCrawler.Core.Proxy;

public static class ProxyFailureClassifier
{
    public static ProxyFailureKind? Classify(int statusCode)
    {
        if (statusCode == 407)
            return ProxyFailureKind.ProxyAuth;
        if (statusCode == 429)
            return ProxyFailureKind.Http429;
        if (statusCode is 500 or 502 or 503 or 504)
            return ProxyFailureKind.Http5xx;
        return null;
    }
}
