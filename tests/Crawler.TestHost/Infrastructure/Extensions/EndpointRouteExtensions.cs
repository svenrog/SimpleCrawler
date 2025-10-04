using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Crawler.TestHost.Infrastructure.Extensions;

public static class EndpointRouteExtensions
{
    public static void MapDefaultHtmlResponse(this IEndpointRouteBuilder routeBuilder, string html)
    {
        routeBuilder.MapGet("/{*path}", (string path) =>
        {
            var extension = Path.GetExtension(path);
            if (extension != null)
                return HttpResults.NotFound();

            return HttpResults.Extensions.Html(html);
        });
    }
}
