using Crawler.TestHost.Infrastructure.Extensions;
using Crawler.TestHost.Infrastructure.Results;
using Crawler.TestHost.Infrastructure.Routing;

namespace Crawler.TestHost.Infrastructure.Factories;

public class JsSpaWebApplicationFactory
{
    public static WebApplication Create(string host, string shellResource)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", host);

        var shellHtml = ResourceHelper.GetHtmlResponse(shellResource);
        var builder = WebApplication.CreateSlimBuilder();

        builder.Services.AddSpaServices();

        var app = builder.Build();

        app.UseMiddleware<EmbeddedResourceStaticFileMiddleware>();
        app.MapDefaultHtmlResponse(shellHtml);

        return app;
    }
}
