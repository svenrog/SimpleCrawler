using SimpleCrawler.Core.Retry;

namespace SimpleCrawler.Tests;

public class RetryClassifierTests
{
    [Theory]
    [InlineData(407, RetryReason.ProxyAuth)]
    [InlineData(429, RetryReason.RateLimited)]
    [InlineData(500, RetryReason.ServerError)]
    [InlineData(502, RetryReason.ServerError)]
    [InlineData(503, RetryReason.ServerError)]
    [InlineData(504, RetryReason.ServerError)]
    public void Classifies_Retryable_Statuses(int status, RetryReason expected)
    {
        Assert.Equal(expected, RetryClassifier.Classify(status));
    }

    [Theory]
    [InlineData(200)]
    [InlineData(301)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(501)]
    public void Returns_Null_For_Non_Retryable_Statuses(int status)
    {
        Assert.Null(RetryClassifier.Classify(status));
    }

    [Fact]
    public void Classifies_Timeout_Exception_As_Timeout()
    {
        Assert.Equal(RetryReason.Timeout, RetryClassifier.Classify(new TimeoutException()));
    }

    [Fact]
    public void Classifies_Other_Exceptions_As_Connection()
    {
        Assert.Equal(RetryReason.Connection, RetryClassifier.Classify(new HttpRequestException("boom")));
    }
}
