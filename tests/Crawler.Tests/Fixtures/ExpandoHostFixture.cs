using Crawler.AngleSharp.Js.Models;
using Crawler.TestHost.Infrastructure.Factories;
using Crawler.TestHost.Infrastructure.Results;
using Crawler.Tests.Assertions;
using Microsoft.AspNetCore.Builder;

namespace Crawler.Tests.Fixtures;

public sealed class ExpandoHostFixture : AbstractHostFixture
{
    public const string HostName = "http://localhost:5291/";
    public static readonly Uri HostUri = new(HostName);

    protected override IEnumerable<WebApplication> CreateHosts()
    {
        return [ExpandoSpaWebApplicationFactory.Create(HostName)];
    }

    protected override JsRenderOptions CreateRenderOptions() => new() { EnableDomExpandos = true };

    // The shell only emits links if a cyclic expando round-trips on a DOM node, so the expected set is the
    // JSON the shell embeds; a matching crawl proves DOM-node expandos work.
    protected override List<string> GetLinks()
    {
        var json = ResourceHelper.GetJsonResponse("default");
        return LinkAssertions.GetJsonLinks(HostUri, json);
    }
}
