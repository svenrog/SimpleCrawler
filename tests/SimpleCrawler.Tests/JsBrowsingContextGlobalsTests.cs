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
          window.__windowIsFunction = typeof Window === 'function';
          window.__windowIsInstance = window instanceof Window;
          window.__documentIsInstance = document instanceof Window;
          window.__brand = Object.prototype.toString.call(window);
          // jQuery's isPlainObject, in the shape 3.x ships it.
          window.__windowIsPlain = (function (obj) {
            var proto, Ctor, hasOwn = Object.prototype.hasOwnProperty, fnToString = hasOwn.toString;
            if (!obj || Object.prototype.toString.call(obj) !== '[object Object]') return false;
            proto = Object.getPrototypeOf(obj);
            if (!proto) return true;
            Ctor = hasOwn.call(proto, 'constructor') && proto.constructor;
            return typeof Ctor === 'function' && fnToString.call(Ctor) === fnToString.call(Object);
          })(window);
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
              stubInstalled: !!window.__stubInstalled,
              windowIsFunction: window.__windowIsFunction,
              windowIsInstance: window.__windowIsInstance,
              documentIsInstance: window.__documentIsInstance,
              brand: window.__brand,
              windowIsPlain: window.__windowIsPlain
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

    // The window's own interface object. The realm's global belongs to the engine, so it cannot be a real
    // instance of anything the prelude declares — but `x instanceof Window` is answerable exactly in a
    // context with no frames, and naming the constructor bare (a `typeof Window` guard, a prototype patch)
    // is a ReferenceError that costs whatever script does it.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task TheWindowInterfaceObject_IdentifiesTheGlobalAndNothingElse(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var ctx = await CollectAsync(engine);

        Assert.True(ctx.GetProperty("windowIsFunction").GetBoolean());
        Assert.True(ctx.GetProperty("windowIsInstance").GetBoolean());
        Assert.False(ctx.GetProperty("documentIsInstance").GetBoolean());
    }

    // The brand a library reads before it decides what the global *is*. An engine global answers
    // "[object Object]" on its own, which jQuery reads as a plain object — and jQuery UI deep-clones a plain
    // option value, so an ordinary `of: window` default sent widget.extend into window.window/self/top/parent
    // and spent the whole stack there, ending the page's scripts before the page had run.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task TheGlobal_IsBrandedAsAWindowRatherThanAPlainObject(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var ctx = await CollectAsync(engine);

        Assert.Equal("[object Window]", ctx.GetProperty("brand").GetString());
        Assert.False(ctx.GetProperty("windowIsPlain").GetBoolean());
    }
}
