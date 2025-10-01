using Crawler.TestHost.Infrastructure.Results;

namespace Crawler.TestHost.Infrastructure.Extensions;

public static class ResultsExtensions
{
    public static IResult Html(this IResultExtensions resultExtensions, string html)
    {
        ArgumentNullException.ThrowIfNull(resultExtensions);

        return new HtmlResult(html);
    }
}
