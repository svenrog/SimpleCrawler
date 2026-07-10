using SimpleCrawler.Js.Models;
using SimpleCrawler.TestHost.Infrastructure.Factories;
using SimpleCrawler.TestHost.Infrastructure.Results;
using SimpleCrawler.Tests.Assertions;
using SimpleCrawler.Tests.Models;
using Microsoft.AspNetCore.Builder;

namespace SimpleCrawler.Tests.Fixtures;

/// <summary>
/// One host per JS-engine capability probe, each on its own port, served from a single fixture (mirroring
/// SpaHostFixture's multi-host layout). Each host assembles Probes/shell.html + a Probes/&lt;script&gt; resource,
/// and only renders its links when the capability behaves — so a crawl that matches the manifest proves it.
/// The fixture enables the superset of opt-in render features so every probe's requirement is satisfied.
/// </summary>
public sealed class ProbeHostFixture : AbstractHostFixture
{
    private const int _basePort = 5280;

    public static string HostName(JsProbeCapability capability) =>
        $"http://localhost:{_basePort + (int)capability}/";

    public static IReadOnlyList<string> LinksFor(JsProbeCapability capability)
    {
        var baseUri = new Uri(HostName(capability));

        // The browser-API shell renders one /features/* link per passing probe; its manifest has no root
        // entry, so the start page the crawler always visits is added explicitly.
        if (capability == JsProbeCapability.BrowserApis)
        {
            return
            [
                HostName(capability),
                .. LinkAssertions.GetJsonLinks(baseUri, ResourceHelper.GetJsonResponse("features")),
            ];
        }

        return LinkAssertions.GetJsonLinks(baseUri, ResourceHelper.GetJsonResponse("default"));
    }

    // DeepWalk guards a Jint-only enumeration policy; V8 host wrappers expose own-enumerable keys and never
    // matched the invariant (but never overflowed), so it runs on Jint only.
    public static IEnumerable<JsEngine> EnginesFor(JsProbeCapability capability)
    {
        return capability switch
        {
            JsProbeCapability.DeepWalk => [JsEngine.Jint],
            _ => [JsEngine.Jint, JsEngine.V8],
        };
    }

    protected override JsRenderOptions CreateRenderOptions() =>
        new() { EnableFetch = true };

    protected override IEnumerable<WebApplication> CreateHosts() =>
        Enum.GetValues<JsProbeCapability>().Select(CreateHost);

    private static WebApplication CreateHost(JsProbeCapability capability)
    {
        var host = HostName(capability);

        return capability switch
        {
            JsProbeCapability.AnchorHref => ProbeSpaWebApplicationFactory.Create(host, "Anchor Href SPA", "anchor-href.js"),
            JsProbeCapability.Expando => ProbeSpaWebApplicationFactory.Create(host, "Expando SPA", "expando.js"),
            JsProbeCapability.Fetch => ProbeSpaWebApplicationFactory.Create(host, "Fetch SPA", "fetch.js", mapLinksJson: true),
            JsProbeCapability.DeferredCallback => ProbeSpaWebApplicationFactory.Create(host, "Deferred Callback SPA", "deferred-callback.js"),
            JsProbeCapability.JQuery => ProbeSpaWebApplicationFactory.Create(host, "jQuery SPA", "jquery.js"),
            JsProbeCapability.BrowserApis => ProbeSpaWebApplicationFactory.Create(host, "Browser APIs SPA", "browser-apis.js"),
            JsProbeCapability.DeepWalk => ProbeSpaWebApplicationFactory.Create(host, "Deep Walk SPA", "deep-walk.js"),
            JsProbeCapability.MapIterator => ProbeSpaWebApplicationFactory.Create(host, "Map Iterator SPA", "map-iterator.js"),
            _ => throw new ArgumentOutOfRangeException(nameof(capability)),
        };
    }
}
