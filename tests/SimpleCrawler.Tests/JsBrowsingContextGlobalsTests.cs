using Microsoft.Extensions.Logging.Abstractions;
using SimpleCrawler.Core.Collectors;
using SimpleCrawler.Js.Models;
using SimpleCrawler.Js.Rendering;
using SimpleCrawler.Tests.Fixtures;
using SimpleCrawler.Tests.Helpers;
using SimpleCrawler.Tests.Models;
using System.Text;
using System.Text.Json;

namespace SimpleCrawler.Tests;

/// <summary>
/// Pins the top-level browsing context's self-referential globals: <c>frames</c>, <c>top</c> and
/// <c>parent</c> are the window itself and <c>length</c> is 0, matching a page with no child frames.
/// <para>
/// A consent stub probes for an already-present CMP with a bare <c>window.frames['__tcfapiLocator']</c> —
/// an indexed read, not a feature test — so an absent <c>frames</c> is a TypeError that aborts the stub's
/// entire script rather than a lookup that misses. Everything the stub would have installed is then gone,
/// silently, which is why this is asserted rather than left to the pages that happen to need it.
/// </para>
/// </summary>
[Collection("Crawler")]
public class JsBrowsingContextGlobalsTests : IClassFixture<JsRendererFixture>
{
    private readonly JsRendererFixture _fixture;

    public JsBrowsingContextGlobalsTests(JsRendererFixture fixture)
    {
        _fixture = fixture;
    }

    private const string _pageUrl = "https://www.example.test/page.html";

    // The IAB TCF locator probe, in the shape the published stub uses.
    private const string _html = """
        <!doctype html><html><head></head><body>
        <script>
          window.__framesIsWindow = window.frames === window;
          window.__topIsWindow = window.top === window;
          window.__parentIsWindow = window.parent === window;
          window.__length = window.length;
          try {
            window.__locator = !!(window.frames['__tcfapiLocator']);
            window.__probeThrew = false;
          } catch (e) {
            window.__probeThrew = true;
          }
          window.__stubInstalled = true;
        </script>
        </body></html>
        """;

    private static readonly string _collectorBlock = DomScriptComposer.CollectorBlock(
    [
        ("ctx", """
            () => ({
              framesIsWindow: window.__framesIsWindow,
              topIsWindow: window.__topIsWindow,
              parentIsWindow: window.__parentIsWindow,
              length: window.__length,
              locator: window.__locator,
              probeThrew: window.__probeThrew,
              stubInstalled: !!window.__stubInstalled
            })
            """),
    ]);

    private async Task<JsonElement> CollectAsync(JsEngine engine)
    {
        var renderer = new JsRenderer(
            _fixture.GetFactory(engine),
            new JsRenderOptions(),
            NullLogger.Instance,
            _collectorBlock);

        using var client = new HttpClient();
        var slices = await renderer.CollectAsync(
            Encoding.UTF8.GetBytes(_html), _pageUrl, client, TestContext.Current.CancellationToken);

        return Assert.Contains("ctx", slices);
    }

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task ATopLevelContext_ReportsFramesTopAndParentAsItself(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var ctx = await CollectAsync(engine);

        Assert.True(ctx.GetProperty("framesIsWindow").GetBoolean());
        Assert.True(ctx.GetProperty("topIsWindow").GetBoolean());
        Assert.True(ctx.GetProperty("parentIsWindow").GetBoolean());
        Assert.Equal(0, ctx.GetProperty("length").GetInt32());
    }

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task AConsentStubProbingForASiblingCmp_FindsNoneAndRunsOn(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var ctx = await CollectAsync(engine);

        Assert.False(ctx.GetProperty("probeThrew").GetBoolean());
        Assert.False(ctx.GetProperty("locator").GetBoolean());
        Assert.True(ctx.GetProperty("stubInstalled").GetBoolean());
    }
}
