using Crawler.TestHost.Infrastructure.Extensions;
using Crawler.TestHost.Infrastructure.Results;

namespace Crawler.TestHost.Infrastructure.Factories;

// A shell that assigns each link through the anchor `href` *property* (not setAttribute) on a freshly
// created anchor whose href is still empty, then reads the reflected HTMLAnchorElement/Axios surface
// (protocol/host/pathname) back. A router or react-helmet does exactly this. The element only renders if
// the assignment did not throw and the parts resolved — guarding the regression where the href setter ran
// `new Uri("")` on the element's own empty href and threw "Invalid URI: The URI is empty.", which surfaced
// through Jint into the bundle and blanked prep.öob.se behind its error boundary.
public class AnchorHrefSpaWebApplicationFactory
{
    private const string _shellTemplate = """
        <!doctype html>
        <html>
            <head><title>Anchor Href SPA</title></head>
            <body>
                <div id="app"></div>
                <script>
                    (function () {
                        var links = __LINKS__;
                        var app = document.getElementById('app');
                        for (var i = 0; i < links.length; i++) {
                            var anchor = document.createElement('a');
                            anchor.href = links[i].href;
                            if (!anchor.protocol || !anchor.host || !anchor.pathname) continue;
                            anchor.textContent = links[i].name;
                            app.appendChild(anchor);
                        }
                    })();
                </script>
            </body>
        </html>
        """;

    public static WebApplication Create(string? host = null)
    {
        if (host != null)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", host);
        }

        var shell = _shellTemplate.Replace("__LINKS__", ResourceHelper.GetJsonResponse("default"));
        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();

        app.MapDefaultHtmlResponse(shell);

        return app;
    }
}
