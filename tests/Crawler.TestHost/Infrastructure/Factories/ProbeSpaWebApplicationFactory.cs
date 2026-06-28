using Crawler.TestHost.Infrastructure.Extensions;
using Crawler.TestHost.Infrastructure.Results;

namespace Crawler.TestHost.Infrastructure.Factories;

// Shared skeleton for the JS-engine capability probes: a shell with an empty #app and a caller-supplied
// script body that only builds anchors when its capability behaves correctly. The body's `__LINKS__` token
// is replaced with the default link manifest, so a crawl that matches that manifest proves the probed
// capability end to end. Each capability's body lives next to its test; this only owns the boilerplate.
public class ProbeSpaWebApplicationFactory
{
    private const string _skeleton = """
        <!doctype html>
        <html>
            <head><title>__TITLE__ SPA</title></head>
            <body>
                <div id="app"></div>
                __BODY__
            </body>
        </html>
        """;

    public static WebApplication Create(string host, string title, string body)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", host);

        var shell = _skeleton
            .Replace("__TITLE__", title)
            .Replace("__BODY__", body)
            .Replace("__LINKS__", ResourceHelper.GetJsonResponse("default"))
            .Replace("__FEATURES__", ResourceHelper.GetJsonResponse("features"));

        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();

        app.MapDefaultHtmlResponse(shell);

        return app;
    }
}
