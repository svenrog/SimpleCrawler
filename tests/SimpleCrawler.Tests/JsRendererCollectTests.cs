using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SimpleCrawler.Core;
using SimpleCrawler.Core.Collectors;
using SimpleCrawler.Core.Models;
using SimpleCrawler.Js.Abstractions;
using SimpleCrawler.Js.Jint;
using SimpleCrawler.Js.Models;
using SimpleCrawler.Js.Rendering;
using SimpleCrawler.Js.V8;
using SimpleCrawler.Tests.Fixtures;
using SimpleCrawler.Tests.Helpers;
using SimpleCrawler.Tests.Models;
using System.Text;
using System.Text.Json;

namespace SimpleCrawler.Tests;

/// <summary>
/// Covers <see cref="JsRenderer.CollectAsync"/> — the collector-slice surface for a consumer that renders
/// without crawling — and the engine-only DI registrations that let such a consumer obtain an
/// <see cref="IJsEngineFactory"/> without standing up a crawl pipeline. Theory'd over both engines, since
/// the collector block is one code path for Jint and V8.
/// </summary>
[Collection("Crawler")]
public class JsRendererCollectTests : IClassFixture<JsRendererFixture>
{
    private readonly JsRendererFixture _fixture;

    public JsRendererCollectTests(JsRendererFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Reads a set of window property paths — the shape a non-crawl consumer registers.</summary>
    private sealed class GlobalsCollector : IRenderedDomCollector
    {
        public string Key => "globals";

        public string DomScript => """
            () => ({
              shop: window.Shopify && window.Shopify.shop,
              jquery: window.jQuery && window.jQuery.fn && window.jQuery.fn.jquery,
              late: typeof window.__late
            })
            """;

        public void OnResponse(UrlReport report, ResponseSignal response) { }

        public ValueTask OnRendered(UrlReport report, JsonElement result, string resolvedUrl) => default;
    }

    /// <summary>A fragment that throws, to prove one bad collector cannot poison the envelope.</summary>
    private sealed class ThrowingCollector : IRenderedDomCollector
    {
        public string Key => "boom";

        public string DomScript => "() => { throw new Error('nope'); }";

        public void OnResponse(UrlReport report, ResponseSignal response) { }

        public ValueTask OnRendered(UrlReport report, JsonElement result, string resolvedUrl) => default;
    }

    private const string _html = """
        <!doctype html><html><head><title>t</title></head><body>
        <script>
          window.Shopify = { shop: "acme.example" };
          window.jQuery = { fn: { jquery: "3.6.0" } };
          setTimeout(function () { window.__late = 1; }, 0);
        </script>
        </body></html>
        """;

    private JsRenderer CreateRenderer(JsEngine engine, params IRenderedDomCollector[] collectors)
    {
        var factory = _fixture.GetFactory(engine);
        var block = DomScriptComposer.CollectorBlock(collectors);
        return new JsRenderer(factory, new JsRenderOptions(), NullLogger.Instance, block);
    }

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task CollectAsync_ReturnsCollectorSlices_FromTheRenderedWindow(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var renderer = CreateRenderer(engine, new GlobalsCollector());

        var slices = await renderer.CollectAsync(
            Encoding.UTF8.GetBytes(_html), "http://localhost:5000/", new HttpClient(), TestContext.Current.CancellationToken);

        var globals = Assert.Contains("globals", slices);
        Assert.Equal("acme.example", globals.GetProperty("shop").GetString());
        Assert.Equal("3.6.0", globals.GetProperty("jquery").GetString());

        // The drain settles timers before finalize, so a global set from a timer is visible to the fragment.
        Assert.Equal("number", globals.GetProperty("late").GetString());
    }

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task CollectAsync_IsolatesAThrowingFragment(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var renderer = CreateRenderer(engine, new ThrowingCollector(), new GlobalsCollector());

        var slices = await renderer.CollectAsync(
            Encoding.UTF8.GetBytes(_html), "http://localhost:5000/", new HttpClient(), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("boom", slices);
        Assert.Contains("globals", slices);
    }

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task CollectAsync_WithNoCollectors_IsEmpty(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var renderer = new JsRenderer(
            _fixture.GetFactory(engine), new JsRenderOptions(), NullLogger.Instance);

        var slices = await renderer.CollectAsync(
            Encoding.UTF8.GetBytes(_html), "http://localhost:5000/", new HttpClient(), TestContext.Current.CancellationToken);

        Assert.Empty(slices);
    }

    // The engine-only registrations must yield a usable factory with no crawler in the container — that is
    // the whole point of them, so a regression would be a consumer forced back to AddV8Crawler.
    [Fact]
    public void AddV8JsEngine_RegistersAnUnkeyedFactory_WithoutACrawler()
    {
        Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var services = new ServiceCollection();
        services.AddV8JsEngine();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IJsEngineFactory>());
        Assert.Null(provider.GetService<ICrawler>());
    }

    [Fact]
    public void AddJintJsEngine_RegistersAnUnkeyedFactory_WithoutACrawler()
    {
        var services = new ServiceCollection();
        services.AddJintJsEngine();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IJsEngineFactory>());
        Assert.Null(provider.GetService<ICrawler>());
    }
}
