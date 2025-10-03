using Crawler.TestHost.Infrastructure.Extensions;
using Crawler.TestHost.Infrastructure.Results;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Crawler.TestHost.Infrastructure.Factories;

public class StaticWebApplicationFactory
{
    public static WebApplication Create(string? host = null)
    {
        if (host != null)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", host);
        }

        var defaultHtml = ResourceHelper.GetHtmlResponse("default");
        var builder = WebApplication.CreateSlimBuilder();

        var app = builder.Build();

        app.MapGet("/{*path}", () => HttpResults.Extensions.Html(defaultHtml));

        return app;
    }
}
