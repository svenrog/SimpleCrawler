using Crawler.TestHost.Infrastructure.Factories;
using Crawler.TestHost.Infrastructure.Results;
using Crawler.Tests.Assertions;
using Microsoft.AspNetCore.Builder;

namespace Crawler.Tests.Fixtures;

public sealed class StaticHostFixture : AbstractHostFixture
{
    protected override WebApplication CreateHost()
    {
        return StaticWebApplicationFactory.Create(HostName);
    }

    protected override List<string> GetLinks()
    {
        var html = ResourceHelper.GetHtmlResponse("default");
        return LinkAssertions.GetHtmlLinks(HostUri, html);
    }
}
