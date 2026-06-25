using Crawler.TestHost.Infrastructure.Factories;
using Crawler.TestHost.Infrastructure.Results;
using Crawler.Tests.Assertions;
using Microsoft.AspNetCore.Builder;

namespace Crawler.Tests.Fixtures;

public sealed class JsTier3HostFixture : AbstractHostFixture
{
    public const string HostName = "http://localhost:5272/";
    public static readonly Uri HostUri = new(HostName);

    protected override WebApplication CreateHost()
    {
        return JsSpaWebApplicationFactory.Create(HostName, "jstier3");
    }

    protected override List<string> GetLinks()
    {
        var json = ResourceHelper.GetJsonResponse("jstier3");
        return LinkAssertions.GetJsonLinks(HostUri, json);
    }
}
