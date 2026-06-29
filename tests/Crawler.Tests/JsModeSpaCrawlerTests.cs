using Crawler.Tests.Assertions;
using Crawler.Tests.Fixtures;
using Crawler.Tests.Helpers;
using Crawler.Tests.Models;

namespace Crawler.Tests;

// Phase 5: the real client-only SPAs hydrate against the pure-JS DOM (DomMode.Js), not just the synthetic
// JsDomRendererTests scripts. Each framework's island must mount and render its <a href> nav so the crawl
// recovers the same link set the Bridge-mode SpaCrawlerTests asserts. Theory'd over both engines.
[Collection("Crawler")]
public class JsModeSpaCrawlerTests : IClassFixture<JsModeSpaHostFixture>
{
    private readonly JsModeSpaHostFixture _context;

    public JsModeSpaCrawlerTests(JsModeSpaHostFixture hostFixture)
    {
        _context = hostFixture;
    }

    public static TheoryData<string, JsEngine> Cases()
    {
        var data = new TheoryData<string, JsEngine>();
        foreach (var framework in JsModeSpaHostFixture.Frameworks)
            foreach (var engine in new[] { JsEngine.Jint, JsEngine.V8 })
                data.Add(framework, engine);

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task JsModeCrawler_Hydrates_And_Crawls(string framework, JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        // Vue renders under V8 but not Jint: its runtime's createRenderer (a re-exported binding) resolves to a
        // non-function under Jint's ES-module evaluation, so the bundle aborts before mounting. This is a Jint
        // module-binding limitation, not a pure-JS DOM gap — the other four frameworks hydrate on both engines.
        if (framework == "vue" && engine == JsEngine.Jint)
            Assert.Skip("Vue's createRenderer import is non-callable under Jint ESM evaluation (renders on V8).");

        var subject = _context.GetJsCrawler(engine);
        var result = await subject.Start(JsModeSpaHostFixture.HostName(framework), _context.CancellationSource.Token);

        LinkAssertions.AssertSameLinks(JsModeSpaHostFixture.LinksFor(framework), result.Urls);
    }
}
