using Crawler.TestHost.Infrastructure.Factories;
using Crawler.TestHost.Infrastructure.Results;
using Crawler.Tests.Assertions;
using Microsoft.AspNetCore.Builder;

namespace Crawler.Tests.Fixtures;

public sealed class JsTier1HostFixture : AbstractHostFixture
{
    public const string HostName = "http://localhost:5270/";
    public static readonly Uri HostUri = new(HostName);

    protected override WebApplication CreateHost()
    {
        return JsSpaWebApplicationFactory.Create(HostName, "jstier1");
    }

    protected override List<string> GetLinks()
    {
        var json = ResourceHelper.GetJsonResponse("jstier1");
        return LinkAssertions.GetJsonLinks(HostUri, json);
    }
}
