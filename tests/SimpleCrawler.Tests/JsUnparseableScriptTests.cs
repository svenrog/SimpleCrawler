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
/// A <c>src</c> answered with something that is not JavaScript must cost that script and nothing more. It is
/// an ordinary thing for a live third-party tag to do — a misconfigured tag is served the site's error page,
/// HTML with a status of 200 — and the whole page's globals rode on it: an external script is parsed outside
/// the engine, so its parse failure arrives as a host exception rather than as the JS <c>SyntaxError</c> an
/// inline script's does, escaping the renderer's per-script isolation.
/// <para>
/// Both engines are asserted because the invariant belongs to the renderer rather than to a backend.
/// </para>
/// </summary>
[Collection("Crawler")]
public class JsUnparseableScriptTests : IClassFixture<JsRendererFixture>
{
    private readonly JsRendererFixture _fixture;

    public JsUnparseableScriptTests(JsRendererFixture fixture)
    {
        _fixture = fixture;
    }

    private const string _pageUrl = "https://www.example.test/";

    /// <summary>
    /// The broken tag is first, and both an ordinary script and a module follow it: each is prepared by its
    /// own path, so only asserting on both proves the failure stayed with the script that caused it.
    /// </summary>
    private const string _html = """
        <!doctype html><html><head>
        <script src="https://www.example.test/tag.js"></script>
        <script src="https://www.example.test/vendor.js"></script>
        <script type="module" src="https://www.example.test/app.mjs"></script>
        </head><body>
        <script>window.__inline = 'ran';</script>
        </body></html>
        """;

    /// <summary>
    /// Serves the site's error page for the broken tag — HTML under a 200, which is what a tag pointed at a
    /// path the site does not have actually receives — and JavaScript for everything else it is asked for.
    /// </summary>
    private sealed class ScriptHost : HttpMessageHandler
    {
        private HttpResponseMessage Respond(HttpRequestMessage request)
        {
            var url = request.RequestUri!.ToString();
            return url switch
            {
                "https://www.example.test/tag.js" => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "<!doctype html><html><body><h1>Page not found</h1></body></html>",
                        Encoding.UTF8,
                        "text/html"),
                },
                "https://www.example.test/vendor.js" => Js("window.__vendor = 'ran';"),
                "https://www.example.test/app.mjs" => Js("window.__module = 'ran';"),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }

        private static HttpResponseMessage Js(string source) =>
            new(HttpStatusCode.OK) { Content = new StringContent(source, Encoding.UTF8, "application/javascript") };

        // Both overloads: the renderer's module/fetch paths call the synchronous Send.
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            => Respond(request);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Respond(request));
    }

    private static readonly string _collectorBlock = DomScriptComposer.CollectorBlock(
    [
        ("globals", """
            () => ({
              vendor: typeof window.__vendor,
              module: typeof window.__module,
              inline: typeof window.__inline
            })
            """),
    ]);

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task AnUnparseableScript_CostsItsOwnScriptAndNotThePage(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var renderer = new JsRenderer(
            _fixture.GetFactory(engine),
            new JsRenderOptions(),
            NullLogger.Instance,
            _collectorBlock);

        using var client = new HttpClient(new ScriptHost());
        var slices = await renderer.CollectAsync(
            Encoding.UTF8.GetBytes(_html), _pageUrl, client, TestContext.Current.CancellationToken);

        var globals = Assert.Contains("globals", slices);

        Assert.Equal("string", globals.GetProperty("vendor").GetString());
        Assert.Equal("string", globals.GetProperty("module").GetString());
        Assert.Equal("string", globals.GetProperty("inline").GetString());
    }
}
