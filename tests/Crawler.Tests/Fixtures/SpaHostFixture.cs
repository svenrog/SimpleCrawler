using Crawler.TestHost.Infrastructure.Factories;
using Crawler.TestHost.Infrastructure.Results;
using Crawler.Tests.Assertions;
using Microsoft.AspNetCore.Builder;

namespace Crawler.Tests.Fixtures;

public sealed class SpaHostFixture : AbstractHostFixture
{
    public static readonly string[] Frameworks = ["react", "preact", "vue", "svelte", "solid"];

    private const int _basePort = 5270;

    public static string HostName(string framework)
    {
        return $"http://localhost:{_basePort + Array.IndexOf(Frameworks, framework)}/";
    }

    // Page links and the browser-API feature links (emitted by feature-nav.ts only when the JS engine
    // implements each API) come from the same JSON the frontend itself consumes.
    public IReadOnlyList<string> LinksFor(string framework)
    {
        var baseUri = new Uri(HostName(framework));

        return
        [
            .. LinkAssertions.GetJsonLinks(baseUri, ResourceHelper.GetJsonResponse("default")),
            .. LinkAssertions.GetJsonLinks(baseUri, ResourceHelper.GetJsonResponse("features")),
        ];
    }

    protected override IEnumerable<WebApplication> CreateHosts()
    {
        return Frameworks.Select(framework => SpaWebApplicationFactory.Create(HostName(framework), framework));
    }
}
