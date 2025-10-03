using Crawler.TestHost.Infrastructure.Factories;
using Crawler.TestHost.Infrastructure.Results;
using Crawler.Tests.Assertions;
using Microsoft.AspNetCore.Builder;

namespace Crawler.Tests.Fixtures;

public sealed class SpaHostFixture : AbstractHostFixture
{
    public const string HostName = "http://localhost:5262/";
    public static readonly Uri HostUri = new(HostName);

    protected override WebApplication CreateHost()
    {
        return SpaWebApplicationFactory.Create(HostName);
    }

    protected override List<string> GetLinks()
    {
        var json = ResourceHelper.GetJsonResponse("default");
        return LinkAssertions.GetJsonLinks(HostUri, json);
    }
}
