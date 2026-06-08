using Crawler.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Crawler.Tests.Fixtures;

public sealed class MetaRobotsHostFixture : AbstractHostFixture
{
    public const string HostName = "http://localhost:5266/";
    public const string HiddenUrl = HostName + "hidden";

    private const string _indexFollow = "index, follow";
    private const string _noIndexFollow = "noindex, follow";

    protected override CrawlerOptions CreateOptions()
    {
        return new CrawlerOptions
        {
            CrawlDelay = 0,
            Concurrency = 4,
            RespectMetaRobots = true,
            RespectRobotsTxt = false,
        };
    }

    protected override WebApplication CreateHost()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", HostName);

        var app = WebApplication.CreateSlimBuilder().Build();

        app.MapGet("/{*path}", (string? path) =>
        {
            // robots.txt / sitemap.xml are absent, so the crawler treats the site as fully allowed.
            if (!string.IsNullOrEmpty(Path.GetExtension(path)))
                return Results.NotFound();

            return path switch
            {
                "hidden" => Results.Content(Page(_noIndexFollow, "/a"), "text/html"),
                "a" => Results.Content(Page(_indexFollow, "/", "/hidden"), "text/html"),
                _ => Results.Content(Page(_indexFollow, "/a", "/hidden"), "text/html"),
            };
        });

        return app;
    }

    private static string Page(string robots, params string[] hrefs)
    {
        var links = string.Concat(hrefs.Select(href => $"<a href=\"{href}\">{href}</a>"));
        return $"<!doctype html><html><head><meta name=\"robots\" content=\"{robots}\" /></head><body>{links}</body></html>";
    }

    protected override List<string> GetLinks()
    {
        // "/hidden" is noindex, so with RespectMetaRobots on it must not appear in the results.
        return [HostName, HostName + "a"];
    }
}
