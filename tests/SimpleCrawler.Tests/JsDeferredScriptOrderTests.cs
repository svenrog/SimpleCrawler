using SimpleCrawler.Js.Models;
using SimpleCrawler.Tests.Fixtures;
using SimpleCrawler.Tests.Helpers;
using SimpleCrawler.Tests.Models;
using System.Net;
using System.Text;

namespace SimpleCrawler.Tests;

/// <summary>
/// Pins that an <c>async</c>/<c>defer</c> external script runs after the document's own inline code rather
/// than where its tag sits. The parser is already past the tag by the time the network answers, so a browser
/// always runs the inline snippets below it first — and a vendor loader is written expecting exactly that:
/// the tag comes first, the snippet that defines the global it writes into comes second.
/// </summary>
[Collection("Crawler")]
public class JsDeferredScriptOrderTests : JsDomRendererTestBase, IClassFixture<JsRendererFixture>
{
    public JsDeferredScriptOrderTests(JsRendererFixture fixture) : base(fixture)
    {
    }

    // The vendor shape: an async loader tag, then the inline snippet that stands up the global it fills in.
    private const string _html = """
        <!doctype html><html><head></head><body>
        <script async src="/vendor/pre-load.js"></script>
        <script>
          window.vendor = { queue: [] };
        </script>
        <script src="/vendor/sync.js"></script>
        </body></html>
        """;

    private sealed class ScriptHost : HttpMessageHandler
    {
        private static HttpResponseMessage Respond(HttpRequestMessage request)
        {
            var body = request.RequestUri!.AbsolutePath switch
            {
                // Each script records itself in the order it ran, and the async one reads the global the
                // inline snippet defines — undefined there is the failure this pins. Running last, it is also
                // the one that writes the record into the tree for the assertion to read.
                "/vendor/pre-load.js" => """
                    window.__ran = (window.__ran || '') + 'async:' + (window.vendor ? 'has' : 'missing') + ';';
                    var probe = document.createElement('a');
                    probe.setAttribute('href', '/probe?ran=' + window.__ran);
                    document.body.appendChild(probe);
                    """,
                "/vendor/sync.js" => "window.__ran = (window.__ran || '') + 'sync;';",
                _ => null,
            };

            return body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/javascript"),
                };
        }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            => Respond(request);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Respond(request));
    }

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_AnAsyncScriptRunsAfterTheDocumentsInlineCode(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var renderer = CreateJsRenderer(engine, new JsRenderOptions { ExecuteCrossOriginScripts = true });
        using var client = new HttpClient(new ScriptHost());
        var result = await renderer.RenderAsync(
            Encoding.UTF8.GetBytes(_html), "https://example.test/", client, TestContext.Current.CancellationToken);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("sync;async:has;", rendered.Replace("\n", string.Empty, StringComparison.Ordinal));
    }
}
