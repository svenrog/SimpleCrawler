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

    public IReadOnlyList<string> LinksFor(string framework)
    {
        var json = ResourceHelper.GetJsonResponse("default");
        return LinkAssertions.GetJsonLinks(new Uri(HostName(framework)), json);
    }

    protected override IEnumerable<WebApplication> CreateHosts()
    {
        return Frameworks.Select(framework => SpaWebApplicationFactory.Create(HostName(framework), framework));
    }
}
