using Crawler.Core;
using Crawler.Core.Helpers;
using Crawler.TestHost.Infrastructure.Factories;
using Crawler.TestHost.Infrastructure.Results;
using Crawler.Tests.Assertions;
using Microsoft.AspNetCore.Builder;

namespace Crawler.Tests.Fixtures;

public sealed class RobotsHostFixture : AbstractHostFixture
{
    public const string HostName = "http://localhost:5264/";
    public static readonly Uri HostUri = new(HostName);

    protected override WebApplication CreateHost()
    {
        return StaticWebApplicationFactory.CreateWithoutLinks(HostName);
    }

    protected override List<string> GetLinks()
    {
        var html = ResourceHelper.GetHtmlResponse("default");
        var links = LinkAssertions.GetHtmlLinks(HostUri, html);

        // This test data is related to the robots.txt file found in Crawler.TestHost/wwwroot/robots.txt
        var exclusions = UriHelper.GetAbsoluteUrls(HostUri, ["/contact", "/book-meeting"]);

        return [.. links.Except(exclusions)];
    }

    protected override CrawlerOptions CreateOptions()
    {
        return new CrawlerOptions
        {
            CrawlDelay = 0,
            Parallelism = 4,
            RespectMetaRobots = true,
            RespectRobotsTxt = true,
        };
    }
}
