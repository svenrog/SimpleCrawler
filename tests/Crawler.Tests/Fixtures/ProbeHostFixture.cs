using Crawler.AngleSharp.Js.Models;
using Crawler.TestHost.Infrastructure.Factories;
using Crawler.TestHost.Infrastructure.Results;
using Crawler.Tests.Assertions;
using Crawler.Tests.Models;
using Microsoft.AspNetCore.Builder;

namespace Crawler.Tests.Fixtures;

// One host per JS-engine capability probe, each on its own port, served from a single fixture (mirroring
// SpaHostFixture's multi-host layout). Each host assembles Probes/shell.html + a Probes/<script> resource,
// and only renders its links when the capability behaves — so a crawl that matches the manifest proves it.
// The fixture enables the superset of opt-in render features so every probe's requirement is satisfied.
public sealed class ProbeHostFixture : AbstractHostFixture
{
    private const int _basePort = 5280;

    public static string HostName(ProbeCapability capability) =>
        $"http://localhost:{_basePort + (int)capability}/";

    public static IReadOnlyList<string> LinksFor(ProbeCapability capability)
    {
        var baseUri = new Uri(HostName(capability));

        // The browser-API shell renders one /features/* link per passing probe; its manifest has no root
        // entry, so the start page the crawler always visits is added explicitly.
        if (capability == ProbeCapability.BrowserApis)
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
    public static IEnumerable<JsEngine> EnginesFor(ProbeCapability capability) =>
        capability == ProbeCapability.DeepWalk ? [JsEngine.Jint] : [JsEngine.Jint, JsEngine.V8];

    protected override JsRenderOptions CreateRenderOptions() =>
        new() { EnableFetch = true, EnableDomExpandos = true };

    protected override IEnumerable<WebApplication> CreateHosts() =>
        Enum.GetValues<ProbeCapability>().Select(CreateHost);

    private static WebApplication CreateHost(ProbeCapability capability)
    {
        var host = HostName(capability);

        return capability switch
        {
            ProbeCapability.AnchorHref => ProbeSpaWebApplicationFactory.Create(host, "Anchor Href SPA", "anchor-href.js"),
            ProbeCapability.Expando => ProbeSpaWebApplicationFactory.Create(host, "Expando SPA", "expando.js"),
            ProbeCapability.Fetch => ProbeSpaWebApplicationFactory.Create(host, "Fetch SPA", "fetch.js", mapLinksJson: true),
            ProbeCapability.DeferredCallback => ProbeSpaWebApplicationFactory.Create(host, "Deferred Callback SPA", "deferred-callback.js"),
            ProbeCapability.JQuery => ProbeSpaWebApplicationFactory.Create(host, "jQuery SPA", "jquery.js"),
            ProbeCapability.BrowserApis => ProbeSpaWebApplicationFactory.Create(host, "Browser APIs SPA", "browser-apis.js"),
            ProbeCapability.DeepWalk => ProbeSpaWebApplicationFactory.Create(host, "Deep Walk SPA", "deep-walk.js"),
            _ => throw new ArgumentOutOfRangeException(nameof(capability)),
        };
    }
}
