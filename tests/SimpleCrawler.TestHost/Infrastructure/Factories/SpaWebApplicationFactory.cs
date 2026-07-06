using SimpleCrawler.TestHost.Infrastructure.Extensions;
using SimpleCrawler.TestHost.Infrastructure.Results;
using SimpleCrawler.TestHost.Infrastructure.Routing;

namespace SimpleCrawler.TestHost.Infrastructure.Factories;

public class SpaWebApplicationFactory
{
    public static WebApplication Create(string? host = null, string framework = "react")
    {
        if (host != null)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", host);
        }

        var spaHtml = ResourceHelper.GetWebRootResource($"{framework}/index.html");
        var builder = WebApplication.CreateSlimBuilder();

        builder.Services.AddSpaServices();

        var app = builder.Build();

        app.UseMiddleware<EmbeddedResourceStaticFileMiddleware>();
        app.MapDefaultHtmlResponse(spaHtml);

        return app;
    }
}
