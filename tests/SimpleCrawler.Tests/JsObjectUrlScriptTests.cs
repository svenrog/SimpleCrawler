using SimpleCrawler.Js.Models;
using SimpleCrawler.Tests.Fixtures;
using SimpleCrawler.Tests.Helpers;
using SimpleCrawler.Tests.Models;
using System.Text;

namespace SimpleCrawler.Tests;

/// <summary>
/// Pins that a &lt;script&gt; whose <c>src</c> the page built with <c>URL.createObjectURL</c> runs from the
/// bytes the page handed over. Nothing about such a source is fetchable — a module shim rewriting its own
/// imports, or a bundler inlining a worker body, builds one — so a renderer that only fetches loses the whole
/// chunk graph behind it. A token the page revoked before connecting the node is the other half: that one a
/// browser cannot fetch either, and neither can this.
/// </summary>
[Collection("Crawler")]
public class JsObjectUrlScriptTests : JsDomRendererTestBase, IClassFixture<JsRendererFixture>
{
    public JsObjectUrlScriptTests(JsRendererFixture fixture) : base(fixture)
    {
    }

    // Revoking the moment the node is connected is what a page does, because in a browser the fetch has
    // already started; the render reads the source at the same point for the same reason.
    private const string _html = """
        <!doctype html><html><head></head><body>
        <script>
        window.marks = [];

        var live = URL.createObjectURL(new Blob(["window.marks.push('via-object-url');"], { type: 'text/javascript' }));
        var script = document.createElement('script');
        script.src = live;
        script.onload = function () { window.marks.push('load-fired'); };
        document.body.appendChild(script);
        URL.revokeObjectURL(live);

        var dead = URL.createObjectURL(new Blob(["window.marks.push('via-revoked-url');"]));
        URL.revokeObjectURL(dead);
        var stale = document.createElement('script');
        stale.src = dead;
        stale.onerror = function () { window.marks.push('error-fired'); };
        document.body.appendChild(stale);

        setTimeout(function () {
          for (var i = 0; i < window.marks.length; i++) {
            var probe = document.createElement('a');
            probe.setAttribute('href', '/ran/' + window.marks[i]);
            document.body.appendChild(probe);
          }
        }, 0);
        </script>
        </body></html>
        """;

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_AScriptBuiltFromAnObjectUrlRunsFromTheBytesThePageHeld(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var renderer = CreateJsRenderer(engine);
        using var client = new HttpClient();
        var result = await renderer.RenderAsync(
            Encoding.UTF8.GetBytes(_html), "https://example.test/", client, TestContext.Current.CancellationToken);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("/ran/via-object-url", rendered, StringComparison.Ordinal);
        Assert.Contains("/ran/load-fired", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("/ran/via-revoked-url", rendered, StringComparison.Ordinal);
        Assert.Contains("/ran/error-fired", rendered, StringComparison.Ordinal);
    }
}
