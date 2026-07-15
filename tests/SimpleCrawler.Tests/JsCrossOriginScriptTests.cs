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
/// Covers <see cref="JsRenderOptions.ExecuteCrossOriginScripts"/>. The page under test is the tag-manager
/// shape: a same-origin container script that, while running, appends a vendor SDK from another host. That
/// SDK is left pending by default (it yields no links and costs a slow cross-origin evaluation), and runs
/// when the option is set — which is what a render that exists to observe what a page *installs* needs,
/// since the SDK's globals are the whole signal.
/// </summary>
[Collection("Crawler")]
public class JsCrossOriginScriptTests : IClassFixture<JsRendererFixture>
{
    private readonly JsRendererFixture _fixture;

    public JsCrossOriginScriptTests(JsRendererFixture fixture)
    {
        _fixture = fixture;
    }

    private const string _pageUrl = "https://www.example.test/";

    private const string _html = """
        <!doctype html><html><head></head><body>
        <script>
          window.__container = 'ran';
          var s = document.createElement('script');
          s.src = 'https://cdn.vendor.test/sdk.js';
          document.head.appendChild(s);
          var own = document.createElement('script');
          own.src = 'https://www.example.test/local.js';
          document.head.appendChild(own);
        </script>
        </body></html>
        """;

    // Serves the two runtime-appended scripts; anything else 404s.
    private sealed class ScriptHost : HttpMessageHandler
    {
        public int CrossOriginRequests { get; private set; }

        private HttpResponseMessage Respond(HttpRequestMessage request)
        {
            var url = request.RequestUri!.ToString();
            if (url == "https://cdn.vendor.test/sdk.js")
            {
                CrossOriginRequests++;
                return Js("window.__vendorSdk = 'loaded';");
            }

            return url == "https://www.example.test/local.js"
                ? Js("window.__localScript = 'loaded';")
                : new HttpResponseMessage(HttpStatusCode.NotFound);
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
              container: typeof window.__container,
              vendor: typeof window.__vendorSdk,
              local: typeof window.__localScript
            })
            """),
    ]);

    private async Task<JsonElement> CollectAsync(JsEngine engine, bool executeCrossOrigin, ScriptHost host)
    {
        var renderer = new JsRenderer(
            _fixture.GetFactory(engine),
            new JsRenderOptions { ExecuteCrossOriginScripts = executeCrossOrigin },
            NullLogger.Instance,
            _collectorBlock);

        using var client = new HttpClient(host);
        var slices = await renderer.CollectAsync(
            Encoding.UTF8.GetBytes(_html), _pageUrl, client, TestContext.Current.CancellationToken);

        return Assert.Contains("globals", slices);
    }

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task ByDefault_ACrossOriginScriptIsNotExecuted(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var host = new ScriptHost();
        var globals = await CollectAsync(engine, executeCrossOrigin: false, host);

        Assert.Equal("string", globals.GetProperty("container").GetString());
        Assert.Equal("string", globals.GetProperty("local").GetString());

        // Left pending: not executed, and never even fetched.
        Assert.Equal("undefined", globals.GetProperty("vendor").GetString());
        Assert.Equal(0, host.CrossOriginRequests);
    }

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task WhenEnabled_ACrossOriginScriptRunsAndItsGlobalsAreVisible(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var host = new ScriptHost();
        var globals = await CollectAsync(engine, executeCrossOrigin: true, host);

        Assert.Equal("string", globals.GetProperty("container").GetString());
        Assert.Equal("string", globals.GetProperty("local").GetString());
        Assert.Equal("string", globals.GetProperty("vendor").GetString());
        Assert.Equal(1, host.CrossOriginRequests);
    }
}
