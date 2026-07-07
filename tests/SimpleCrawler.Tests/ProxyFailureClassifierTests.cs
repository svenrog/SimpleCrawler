using SimpleCrawler.Core.Proxy;

namespace SimpleCrawler.Tests;

public class ProxyFailureClassifierTests
{
    [Theory]
    [InlineData(407, ProxyFailureKind.ProxyAuth)]
    [InlineData(429, ProxyFailureKind.Http429)]
    [InlineData(500, ProxyFailureKind.Http5xx)]
    [InlineData(502, ProxyFailureKind.Http5xx)]
    [InlineData(503, ProxyFailureKind.Http5xx)]
    [InlineData(504, ProxyFailureKind.Http5xx)]
    public void Classifies_Retryable_Statuses(int status, ProxyFailureKind expected)
    {
        Assert.Equal(expected, ProxyFailureClassifier.Classify(status));
    }

    [Theory]
    [InlineData(200)]
    [InlineData(301)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(501)]
    public void Returns_Null_For_Non_Retryable_Statuses(int status)
    {
        Assert.Null(ProxyFailureClassifier.Classify(status));
    }
}
