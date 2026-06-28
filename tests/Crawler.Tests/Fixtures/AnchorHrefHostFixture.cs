using Crawler.AngleSharp.Js.Models;
using Crawler.TestHost.Infrastructure.Factories;
using Crawler.TestHost.Infrastructure.Results;
using Crawler.Tests.Assertions;
using Microsoft.AspNetCore.Builder;

namespace Crawler.Tests.Fixtures;

public sealed class AnchorHrefHostFixture : AbstractHostFixture
{
    public const string HostName = "http://localhost:5292/";
    public static readonly Uri HostUri = new(HostName);

    protected override IEnumerable<WebApplication> CreateHosts()
    {
        return [AnchorHrefSpaWebApplicationFactory.Create(HostName)];
    }

    protected override JsRenderOptions CreateRenderOptions() => new() { EnableDomExpandos = true };

    // The shell only emits links once `anchor.href = url` assigns without throwing and the reflected URL
    // parts resolve, so the expected set is the JSON the shell embeds; a matching crawl proves the anchor
    // href setter and the lazy protocol/host/pathname getters round-trip.
    protected override List<string> GetLinks()
    {
        var json = ResourceHelper.GetJsonResponse("default");
        return LinkAssertions.GetJsonLinks(HostUri, json);
    }
}
