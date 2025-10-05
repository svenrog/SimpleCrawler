using Crawler.TestHost.Infrastructure.Extensions;
using Crawler.TestHost.Infrastructure.Results;
using Crawler.TestHost.Infrastructure.Routing;

namespace Crawler.TestHost.Infrastructure.Factories;

public class StaticWebApplicationFactory
{
    public static WebApplication Create(string? host = null)
    {
        return Create(host, "default");
    }

    public static WebApplication CreateWithoutLinks(string? host = null)
    {
        return Create(host, "nolinks");
    }

    private static WebApplication Create(string? host, string responseName)
    {
        if (host != null)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", host);
        }

        var defaultHtml = ResourceHelper.GetHtmlResponse(responseName);
        var builder = WebApplication.CreateSlimBuilder();

        builder.Services.AddStaticServices();

        var app = builder.Build();

        app.UseMiddleware<EmbeddedResourceStaticFileMiddleware>();
        app.MapDefaultHtmlResponse(defaultHtml);

        return app;
    }
}
