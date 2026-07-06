using Crawler.TestHost.Infrastructure.Factories;
using Crawler.TestHost.Infrastructure.Results;
using Crawler.Tests.Assertions;
using Microsoft.AspNetCore.Builder;

namespace Crawler.Tests.Fixtures;

public sealed class StaticHostFixture : AbstractHostFixture
{
    public const string HostName = "http://localhost:5260/";
    public static readonly Uri HostUri = new(HostName);

    protected override IEnumerable<WebApplication> CreateHosts()
    {
        return [StaticWebApplicationFactory.Create(HostName)];
    }

    protected override List<string> GetLinks()
    {
        var html = ResourceHelper.GetHtmlResponse("default");
        return LinkAssertions.GetHtmlLinks(HostUri, html);
    }
}
