namespace SimpleCrawler.Core.Extensions;

public static class HttpResponseMessageExtensions
{
    public static bool IsSuccessStatus(this HttpResponseMessage response)
        => (int)response.StatusCode is >= 200 and <= 299;
}
