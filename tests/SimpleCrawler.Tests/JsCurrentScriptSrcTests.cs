using Microsoft.Extensions.Logging.Abstractions;
using SimpleCrawler.Core.Collectors;
using SimpleCrawler.Js.Models;
using SimpleCrawler.Js.Rendering;
using SimpleCrawler.Tests.Fixtures;
using SimpleCrawler.Tests.Helpers;
using SimpleCrawler.Tests.Models;
using System.Net;
using System.Text;
using System.Text.Json;

namespace SimpleCrawler.Tests;

/// <summary>
/// Pins the reflected-URL split on a script's <c>src</c>, for the initial-HTML path and the
/// runtime-appended path alike: <c>getAttribute("src")</c> hands back the literal string the markup authored,
/// while the <c>.src</c> property resolves it against the document base. A browser distinguishes the two and
/// bundlers read *both*, so collapsing them onto one value satisfies one consumer by breaking the other —
/// webpack's auto-public-path wants the resolved URL off <c>.src</c>, while Turbopack derives a chunk's
/// identity by stripping its configured base path off <c>getAttribute("src")</c> and keeps the whole string
/// when the prefix does not match.
/// <para>
/// This is a tripwire rather than a unit test because of how the failure presents: the resolved URL fails
/// Turbopack's prefix test, so each chunk registers under a key nothing awaits, the entry module's dependency
/// gate never settles, and the app never hydrates — every chunk fetched, every script executed, nothing
/// thrown, and no global set. Downstream that is indistinguishable from a site not using the technology, so
/// only an assertion at this seam can catch it.
/// </para>
/// </summary>
[Collection("Crawler")]
public class JsCurrentScriptSrcTests : IClassFixture<JsRendererFixture>
{
    private readonly JsRendererFixture _fixture;

    public JsCurrentScriptSrcTests(JsRendererFixture fixture)
    {
        _fixture = fixture;
    }

    private const string _pageUrl = "https://www.example.test/nested/page.html";

    // Both scripts carry a root-relative src, the shape a chunk runtime's base-path strip expects.
    private const string _html = """
        <!doctype html><html><head></head><body>
        <script src="/chunks/entry.js"></script>
        <script>
          var s = document.createElement('script');
          s.src = '/chunks/lazy.js';
          document.head.appendChild(s);
        </script>
        </body></html>
        """;

    // Each chunk records what it sees on document.currentScript while it runs.
    private const string _record = """
        (function () {
          var c = document.currentScript;
          window.__seen = window.__seen || {};
          window.__seen[{0}] = { attr: c && c.getAttribute('src'), prop: c && c.src };
        })();
        """;

    private sealed class ScriptHost : HttpMessageHandler
    {
        private HttpResponseMessage Respond(HttpRequestMessage request)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/chunks/entry.js")
                return Js(_record.Replace("{0}", "'entry'"));

            return path == "/chunks/lazy.js"
                ? Js(_record.Replace("{0}", "'lazy'"))
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Js(string source) =>
            new(HttpStatusCode.OK) { Content = new StringContent(source, Encoding.UTF8, "application/javascript") };

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            => Respond(request);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Respond(request));
    }

    private static readonly string _collectorBlock = DomScriptComposer.CollectorBlock(
    [
        ("seen", """
            () => ({
              entryAttr: (window.__seen && window.__seen.entry || {}).attr,
              entryProp: (window.__seen && window.__seen.entry || {}).prop,
              lazyAttr: (window.__seen && window.__seen.lazy || {}).attr,
              lazyProp: (window.__seen && window.__seen.lazy || {}).prop
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

        using var client = new HttpClient(new ScriptHost());
        var slices = await renderer.CollectAsync(
            Encoding.UTF8.GetBytes(_html), _pageUrl, client, TestContext.Current.CancellationToken);

        return Assert.Contains("seen", slices);
    }

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task AScriptFromTheInitialHtml_SeesItsLiteralSrcAttributeAndAResolvedSrcProperty(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var seen = await CollectAsync(engine);

        Assert.Equal("/chunks/entry.js", seen.GetProperty("entryAttr").GetString());
        Assert.Equal("https://www.example.test/chunks/entry.js", seen.GetProperty("entryProp").GetString());
    }

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task ARuntimeAppendedChunk_SeesItsLiteralSrcAttributeAndAResolvedSrcProperty(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var seen = await CollectAsync(engine);

        Assert.Equal("/chunks/lazy.js", seen.GetProperty("lazyAttr").GetString());
        Assert.Equal("https://www.example.test/chunks/lazy.js", seen.GetProperty("lazyProp").GetString());
    }
}
