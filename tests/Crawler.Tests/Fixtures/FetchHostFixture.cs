using Crawler.AngleSharp.Js.Models;
using Crawler.TestHost.Infrastructure.Factories;
using Crawler.TestHost.Infrastructure.Results;
using Crawler.Tests.Assertions;
using Microsoft.AspNetCore.Builder;

namespace Crawler.Tests.Fixtures;

public sealed class FetchHostFixture : AbstractHostFixture
{
    public const string HostName = "http://localhost:5290/";
    public static readonly Uri HostUri = new(HostName);

    protected override IEnumerable<WebApplication> CreateHosts()
    {
        return [FetchSpaWebApplicationFactory.Create(HostName)];
    }

    protected override JsRenderOptions CreateRenderOptions() => new() { EnableFetch = true };

    // The shell serves no links; they only exist in the JSON the inline script fetches at runtime, so
    // the expected set is exactly that JSON. A crawl matching it proves the engine ran the fetch.
    protected override List<string> GetLinks()
    {
        var json = ResourceHelper.GetJsonResponse("default");
        return LinkAssertions.GetJsonLinks(HostUri, json);
    }
}
