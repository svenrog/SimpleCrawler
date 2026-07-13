using SimpleCrawler.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace SimpleCrawler.Tests.Fixtures;

/// <summary>
/// A single-page host carrying one of every signal PageSignals collects (meta tags, a script src, a
/// JSON-LD block, and a Set-Cookie header), crawled with CapturePageSignals on across all six backends.
/// </summary>
public sealed class SignalsHostFixture : AbstractHostFixture
{
    public const string HostName = "http://localhost:5290/";

    private const string _page = """
        <!doctype html>
        <html>
            <head>
                <meta name="robots" content="index, follow" />
                <meta name="generator" content="SimpleCrawler test host" />
                <meta property="og:title" content="Signals Page" />
                <script src="/app.js"></script>
                <script type="application/ld+json">{"@type":"Organization"}</script>
            </head>
            <body>
                <a href="/a">a</a>
            </body>
        </html>
        """;

    protected override CrawlerOptions CreateOptions()
    {
        return new CrawlerOptions
        {
            CrawlDelay = 0,
            Concurrency = 4,
            RespectMetaRobots = false,
            RespectRobotsTxt = false,
            EnableSitemapDiscovery = false,
            CapturePageSignals = true,
        };
    }

    protected override IEnumerable<WebApplication> CreateHosts()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", HostName);

        var app = WebApplication.CreateSlimBuilder().Build();

        app.MapGet("/{*path}", (HttpContext context, string? path) =>
        {
            context.Response.Headers.Append("Set-Cookie", "session=abc123; Path=/; HttpOnly");

            // A real JS response, not the HTML fallback: Jint aborts the whole render (rather than logging
            // and continuing, like V8) if an external <script src> resolves to non-JS content.
            if (path == "app.js")
                return Results.Content("// no-op", "application/javascript");

            return Results.Content(_page, "text/html");
        });

        return [app];
    }

    protected override List<string> GetLinks() => [HostName, HostName + "a"];
}
