using Crawler.TestHost.Infrastructure.Extensions;
using Crawler.TestHost.Infrastructure.Results;
using Crawler.TestHost.Infrastructure.Routing;

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

        builder.Services.AddSpaServices();

        var app = builder.Build();

        app.UseMiddleware<EmbeddedResourceStaticFileMiddleware>();
        app.MapDefaultHtmlResponse(spaHtml);

        return app;
    }
}
