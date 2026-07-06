using SimpleCrawler.TestHost.Infrastructure.Factories;
using SimpleCrawler.TestHost.Infrastructure.Results;
using SimpleCrawler.Tests.Assertions;
using Microsoft.AspNetCore.Builder;

namespace SimpleCrawler.Tests.Fixtures;

public sealed class SpaHostFixture : AbstractHostFixture
{
    public static readonly string[] Frameworks = ["react", "preact", "vue", "svelte", "solid"];

    private const int _basePort = 5270;

    public static string HostName(string framework)
    {
        return $"http://localhost:{_basePort + Array.IndexOf(Frameworks, framework)}/";
    }

    // The page links come from the same JSON the frontend itself consumes; this fixture covers real
    // framework rendering only. Paging navigates between these nav routes (each route is one catalog
    // bucket), so the crawlable URL set is exactly the nav set — no extra pagination URLs to expect.
    public static IReadOnlyList<string> LinksFor(string framework)
    {
        var baseUri = new Uri(HostName(framework));
        return LinkAssertions.GetJsonLinks(baseUri, ResourceHelper.GetJsonResponse("default"));
    }

    protected override IEnumerable<WebApplication> CreateHosts()
    {
        return Frameworks.Select(framework => SpaWebApplicationFactory.Create(HostName(framework), framework));
    }
}
