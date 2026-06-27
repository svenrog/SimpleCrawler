using Crawler.TestHost.Infrastructure.Extensions;
using Crawler.TestHost.Infrastructure.Results;

namespace Crawler.TestHost.Infrastructure.Factories;

// A shell whose links are only built if the engine can store (and read back) expando properties on DOM
// nodes the way React 18 stashes fibers — including a cyclic object graph. If EnableDomExpandos is off
// (plain wrappers reject the assignment) the probe fails and no links render, so the crawl result proves
// the expando path end to end.
public class ExpandoSpaWebApplicationFactory
{
    private const string _shellTemplate = """
        <!doctype html>
        <html>
            <head><title>Expando SPA</title></head>
            <body>
                <div id="app"></div>
                <script>
                    (function () {
                        var probe = document.createElement('div');
                        var cyclic = {}; cyclic.self = cyclic;
                        probe.__fiber = cyclic;
                        document.__root = probe;
                        var ok = probe.__fiber === cyclic
                            && probe.__fiber.self === probe.__fiber
                            && document.__root === probe;
                        if (!ok) return;
                        var links = __LINKS__;
                        var app = document.getElementById('app');
                        for (var i = 0; i < links.length; i++) {
                            var anchor = document.createElement('a');
                            anchor.setAttribute('href', links[i].href);
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
