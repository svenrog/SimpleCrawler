using Crawler.TestHost.Infrastructure.Extensions;
using Crawler.TestHost.Infrastructure.Results;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Crawler.TestHost.Infrastructure.Factories;

// A deliberately minimal SPA whose links exist nowhere in the served HTML: the shell ships an inline
// script that fetch()es a same-origin JSON document and builds the anchors at runtime. It exercises the
// JS engines' network-backed fetch (and Response.json()) — the Astro SPAs bundle their data via eager
// glob, so this is the only host that proves a real runtime fetch renders.
public class FetchSpaWebApplicationFactory
{
    private const string _shell = """
        <!doctype html>
        <html>
            <head><title>Fetch SPA</title></head>
            <body>
                <div id="app"></div>
                <script>
                    fetch('/links.json')
                        .then(function (response) { return response.json(); })
                        .then(function (links) {
                            var app = document.getElementById('app');
                            for (var i = 0; i < links.length; i++) {
                                var anchor = document.createElement('a');
                                anchor.setAttribute('href', links[i].href);
                                anchor.textContent = links[i].name;
                                app.appendChild(anchor);
                            }
                        });
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

        var links = ResourceHelper.GetJsonResponse("default");
        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();

        app.MapGet("/links.json", () => HttpResults.Text(links, "application/json"));
        app.MapDefaultHtmlResponse(_shell);

        return app;
    }
}
