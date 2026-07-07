using System.Net;

namespace SimpleCrawler.Core.Extensions;

public static class HttpStatusExtensions
{
    public static bool IsSuccessStatus(this HttpResponseMessage response)
        => response.StatusCode.IsSuccessStatus();

    public static bool IsSuccessStatus(this HttpStatusCode statusCode)
        => ((int)statusCode).IsSuccessStatus();

    public static bool IsSuccessStatus(this int statusCode)
        => statusCode is >= 200 and <= 299;
}
