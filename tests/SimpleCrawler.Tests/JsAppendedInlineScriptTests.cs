using SimpleCrawler.Js.Models;
using SimpleCrawler.Tests.Fixtures;
using SimpleCrawler.Tests.Helpers;
using SimpleCrawler.Tests.Models;
using System.Text;

namespace SimpleCrawler.Tests;

/// <summary>
/// Pins that a &lt;script&gt; the page connects after parse runs its own source. A browser runs it the moment
/// it is connected, by every route a page uses to connect one; a renderer that only fetches <c>src</c> leaves
/// a tag manager's snippet, a loader re-adding a script it lifted out of the markup, and everything those
/// define as silently dead code — which surfaces far away, as somebody else's missing global.
/// </summary>
[Collection("Crawler")]
public class JsAppendedInlineScriptTests : JsDomRendererTestBase, IClassFixture<JsRendererFixture>
{
    public JsAppendedInlineScriptTests(JsRendererFixture fixture) : base(fixture)
    {
    }

    // The four routes, and the one thing each must not be: skipped. The record is written into an anchor so
    // the assertion reads it off the serialized tree.
    private const string _html = """
        <!doctype html><html><head></head><body>
        <script>
        window.marks = [];

        var byTextContent = document.createElement('script');
        byTextContent.textContent = "window.marks.push('via-text-content');";
        document.body.appendChild(byTextContent);

        var byText = document.createElement('script');
        byText.text = "window.marks.push('via-text-idl');";
        document.head.appendChild(byText);

        var byNamespace = document.createElementNS('http://www.w3.org/1999/xhtml', 'script');
        byNamespace.textContent = "window.marks.push('via-create-element-ns');";
        document.body.appendChild(byNamespace);

        document.write('<scr' + 'ipt>window.marks.push("via-document-write");</scr' + 'ipt>');

        var inert = document.createElement('script');
        inert.type = 'application/ld+json';
        inert.textContent = "window.marks.push('via-json-ld');";
        document.body.appendChild(inert);

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
    public async Task JsMode_AnAppendedInlineScriptRunsByEveryRouteThatConnectsOne(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var renderer = CreateJsRenderer(engine);
        using var client = new HttpClient();
        var result = await renderer.RenderAsync(
            Encoding.UTF8.GetBytes(_html), "https://example.test/", client, TestContext.Current.CancellationToken);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("/ran/via-text-content", rendered, StringComparison.Ordinal);
        Assert.Contains("/ran/via-text-idl", rendered, StringComparison.Ordinal);
        Assert.Contains("/ran/via-create-element-ns", rendered, StringComparison.Ordinal);
        Assert.Contains("/ran/via-document-write", rendered, StringComparison.Ordinal);
        // A type no browser executes stays data, here as everywhere else.
        Assert.DoesNotContain("/ran/via-json-ld", rendered, StringComparison.Ordinal);
    }
}
