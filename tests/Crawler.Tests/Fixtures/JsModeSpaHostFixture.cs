using Crawler.Js.Models;
using Crawler.Core;
using Crawler.TestHost.Infrastructure.Factories;
using Crawler.TestHost.Infrastructure.Results;
using Crawler.Tests.Assertions;
using Microsoft.AspNetCore.Builder;

namespace Crawler.Tests.Fixtures;

// Same five client-only SPA hosts as SpaHostFixture, rendered through the pure-JS DOM (dom.js).
// The Phase-5 guard that real frameworks hydrate against dom.js. Its own port range keeps it from
// colliding with the SpaHostFixture in the shared "Crawler" collection.
//
// Sitemap discovery is OFF here on purpose: the sitemap.xml these hosts serve lists every default.json link,
// so with it on the link-parity assertion would pass from the sitemap alone — even if nothing rendered. With
// it off the only source of links is the hydrated nav the bundle paints, so the test actually proves the
// framework mounted against dom.js.
public sealed class JsModeSpaHostFixture : AbstractHostFixture
{
    public static readonly string[] Frameworks = SpaHostFixture.Frameworks;

    private const int _basePort = 5290;

    public static string HostName(string framework) =>
        $"http://localhost:{_basePort + Array.IndexOf(Frameworks, framework)}/";

    public static IReadOnlyList<string> LinksFor(string framework)
    {
        var baseUri = new Uri(HostName(framework));
        return LinkAssertions.GetJsonLinks(baseUri, ResourceHelper.GetJsonResponse("default"));
    }

    protected override CrawlerOptions CreateOptions()
    {
        var options = base.CreateOptions();
        options.EnableSitemapDiscovery = false;
        return options;
    }

    protected override JsRenderOptions CreateRenderOptions() =>
        new();

    protected override IEnumerable<WebApplication> CreateHosts() =>
        Frameworks.Select(framework => SpaWebApplicationFactory.Create(HostName(framework), framework));
}
