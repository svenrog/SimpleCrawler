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
/// Pins <c>&lt;meta&gt;</c>'s reflected <c>content</c> property, for the initial-HTML path and the
/// created-at-runtime path alike. Pages park bootstrap JSON in a meta tag and read it back as a property
/// (<c>JSON.parse(meta.content)</c>), never as an attribute.
/// <para>
/// This is a tripwire rather than a unit test because of how the failure presents: the element resolves, so
/// the read is <c>JSON.parse(undefined)</c> — a SyntaxError thrown during render, which a framework turns
/// into a client-side exception and an error route. The container is emptied, hydration never commits, and
/// so no effect ever runs and nothing the page would have mounted appears. Every script is fetched and
/// executed and no global is set, which downstream is indistinguishable from a site not using the
/// technology, so only an assertion at this seam can catch it.
/// </para>
/// </summary>
[Collection("Crawler")]
public class JsMetaContentTests : IClassFixture<JsRendererFixture>
{
    private readonly JsRendererFixture _fixture;

    public JsMetaContentTests(JsRendererFixture fixture)
    {
        _fixture = fixture;
    }

    private const string _pageUrl = "https://www.example.test/page.html";

    // The bootstrap-data-in-a-meta shape, read back through the property exactly as a page does.
    private const string _html = """
        <!doctype html><html><head>
        <meta id="bootstrap" name="app-config" content='{"locale":"en","flag":true}'>
        <meta http-equiv="content-language" content="en-GB">
        </head><body>
        <script>
          var el = document.getElementById('bootstrap');
          window.__parsed = el ? JSON.parse(el.content) : null;
          window.__name = el && el.name;
          window.__equiv = document.querySelector('[http-equiv]').httpEquiv;

          var made = document.createElement('meta');
          made.content = '{"made":1}';
          made.name = 'runtime';
          document.head.appendChild(made);
          window.__madeAttr = made.getAttribute('content');
          window.__madeProp = made.content;
        </script>
        </body></html>
        """;

    private static readonly string _collectorBlock = DomScriptComposer.CollectorBlock(
    [
        ("meta", """
            () => ({
              locale: window.__parsed && window.__parsed.locale,
              name: window.__name,
              equiv: window.__equiv,
              madeAttr: window.__madeAttr,
              madeProp: window.__madeProp
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

        return Assert.Contains("meta", slices);
    }

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task AMetaFromTheInitialHtml_ReflectsItsContentAttributeAsAProperty(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var meta = await CollectAsync(engine);

        Assert.Equal("en", meta.GetProperty("locale").GetString());
        Assert.Equal("app-config", meta.GetProperty("name").GetString());
        Assert.Equal("content-language", meta.GetProperty("equiv").GetString());
    }

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task AMetaCreatedAtRuntime_ReflectsAnAssignedContentBackToTheAttribute(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var meta = await CollectAsync(engine);

        Assert.Equal("{\"made\":1}", meta.GetProperty("madeAttr").GetString());
        Assert.Equal("{\"made\":1}", meta.GetProperty("madeProp").GetString());
    }
}
