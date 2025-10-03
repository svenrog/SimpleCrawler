using Crawler.TestHost.Infrastructure.Extensions;
using Crawler.TestHost.Infrastructure.Results;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Crawler.TestHost.Infrastructure.Factories;

public class SpaWebApplicationFactory
{
    public static WebApplication Create(string? host = null)
    {
        if (host != null)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", host);
        }

        var spaHtml = ResourceHelper.GetWebRootResource("index.html");
        var builder = WebApplication.CreateSlimBuilder();

        //TODO: Fix support for embedded resources
        // Current solution will not work inside test project
        builder.WebHost.UseStaticWebAssets();

        var app = builder.Build();

        // Current solution will not work inside test project
        app.MapStaticAssets().ShortCircuit();

        app.MapGet("/{*path}", () => HttpResults.Extensions.Html(spaHtml));

        return app;
    }
}
