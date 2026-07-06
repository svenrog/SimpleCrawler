using SimpleCrawler.Core;
using SimpleCrawler.Core.Helpers;
using SimpleCrawler.TestHost.Infrastructure.Factories;
using SimpleCrawler.TestHost.Infrastructure.Results;
using SimpleCrawler.Tests.Assertions;
using Microsoft.AspNetCore.Builder;

namespace SimpleCrawler.Tests.Fixtures;

public sealed class RobotsHostFixture : AbstractHostFixture
{
    public const string HostName = "http://localhost:5264/";
    public static readonly Uri HostUri = new(HostName);

    protected override IEnumerable<WebApplication> CreateHosts()
    {
        return [StaticWebApplicationFactory.CreateWithoutLinks(HostName)];
    }

    protected override List<string> GetLinks()
    {
        var html = ResourceHelper.GetHtmlResponse("default");
        var links = LinkAssertions.GetHtmlLinks(HostUri, html);

        // This test data is related to the robots.txt file found in SimpleCrawler.TestHost/wwwroot/robots.txt
        var exclusions = UriHelper.GetAbsoluteUrls(HostUri, ["/contact", "/book-meeting"]);

        return [.. links.Except(exclusions)];
    }

    protected override CrawlerOptions CreateOptions()
    {
        return new CrawlerOptions
        {
            CrawlDelay = 0,
            Concurrency = 4,
            RespectMetaRobots = true,
            RespectRobotsTxt = true,
        };
    }
}
