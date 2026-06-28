using Crawler.Tests.Assertions;
using Crawler.Tests.Fixtures;
using Crawler.Tests.Helpers;

namespace Crawler.Tests;

// One test per (capability, engine): each capability shell renders its links only when the probed JS-engine
// behaviour works, so a crawl that matches the embedded manifest is the regression guard. See ProbeHostFixture
// for which real-site bug each capability encodes.
[Collection("Crawler")]
public class ProbeCrawlerTests : IClassFixture<ProbeHostFixture>
{
    private readonly ProbeHostFixture _context;

    public ProbeCrawlerTests(ProbeHostFixture hostFixture)
    {
        _context = hostFixture;
    }

    public static TheoryData<ProbeCapability, JsEngine> Cases()
    {
        var data = new TheoryData<ProbeCapability, JsEngine>();
        foreach (var capability in Enum.GetValues<ProbeCapability>())
            foreach (var engine in ProbeHostFixture.EnginesFor(capability))
                data.Add(capability, engine);

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Crawler_Renders_Capability(ProbeCapability capability, JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var subject = _context.GetJsCrawler(engine);
        var result = await subject.Start(ProbeHostFixture.HostName(capability), _context.CancellationSource.Token);

        var expected = ProbeHostFixture.LinksFor(capability);
        Assert.NotEmpty(expected);
        LinkAssertions.AssertSameLinks(expected, result.Urls);
    }
}
