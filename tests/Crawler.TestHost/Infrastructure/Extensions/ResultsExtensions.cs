using Crawler.TestHost.Infrastructure.Results;

namespace Crawler.TestHost.Infrastructure.Extensions;

public static class ResultsExtensions
{
    public static IResult Html(this IResultExtensions resultExtensions, string html)
    {
        ArgumentNullException.ThrowIfNull(resultExtensions);

        return new HtmlResult(html);
    }

    public static IResult Raw(this IResultExtensions resultExtensions, byte[] bytes, string mimeType)
    {
        ArgumentNullException.ThrowIfNull(resultExtensions);

        return new RawResult(bytes, mimeType);
    }

    public static IResult SerializedJson(this IResultExtensions resultExtensions, string json)
    {
        ArgumentNullException.ThrowIfNull(resultExtensions);

        return new SerializedJsonResult(json);
    }
}
