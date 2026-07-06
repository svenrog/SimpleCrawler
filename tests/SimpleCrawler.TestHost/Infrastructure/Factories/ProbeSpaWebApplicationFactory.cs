using SimpleCrawler.TestHost.Infrastructure.Extensions;
using SimpleCrawler.TestHost.Infrastructure.Results;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace SimpleCrawler.TestHost.Infrastructure.Factories;

// Assembles a JS-engine capability probe from embedded resources: the shared Probes/shell.html template plus
// a single Probes/<script> that only builds the anchors when its capability behaves correctly. The shell
// exposes the link manifest as window.__links__ / window.__features__, so a crawl that matches the manifest
// proves the probed capability. Each script (with its regression rationale) lives next to the template.
public class ProbeSpaWebApplicationFactory
{
    public static WebApplication Create(string host, string title, string script, bool mapLinksJson = false)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", host);

        var shell = ResourceHelper.GetProbeResource("shell.html")
            .Replace("__TITLE__", title)
            .Replace("__SCRIPT__", ResourceHelper.GetProbeResource(script))
            .Replace("__LINKS__", ResourceHelper.GetJsonResponse("default"))
            .Replace("__FEATURES__", ResourceHelper.GetJsonResponse("features"));

        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();

        if (mapLinksJson)
            app.MapGet("/links.json", () => HttpResults.Text(ResourceHelper.GetJsonResponse("default"), "application/json"));

        app.MapDefaultHtmlResponse(shell);

        return app;
    }
}
