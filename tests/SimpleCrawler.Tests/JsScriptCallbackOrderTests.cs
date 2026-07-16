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
/// Pins that <see cref="XMLHttpRequest"/>'s completion callbacks fire off the event loop rather than inline
/// inside <c>send()</c>, matching a browser. The ordering is load-bearing rather than pedantic: a script
/// routinely issues a request near its top and assigns what the handler depends on further down, so firing
/// <c>onload</c> synchronously runs the handler against a half-initialized script and throws — and the shim
/// swallows that, leaving a stub that simply never does its work and no error anyone downstream can see.
/// </summary>
[Collection("Crawler")]
public class JsScriptCallbackOrderTests : IClassFixture<JsRendererFixture>
{
    private readonly JsRendererFixture _fixture;

    public JsScriptCallbackOrderTests(JsRendererFixture fixture)
    {
        _fixture = fixture;
    }

    private const string _pageUrl = "https://www.example.test/";

    // The shape that breaks under a synchronous fire: onload depends on a function assigned below send().
    private const string _html = """
        <!doctype html><html><head></head><body>
        <script>
          window.__order = [];
          var x = new XMLHttpRequest();
          x.open('GET', '/geo.json');
          x.onload = function () {
            window.__order.push('onload');
            try { window.__lateInit(); }
            catch (e) { window.__order.push('threw:' + e.message); }
          };
          x.send();
          window.__order.push('after-send');
          window.__lateInit = function () { window.__order.push('lateInit-ran'); };
        </script>
        </body></html>
        """;

    private sealed class GeoHost : HttpMessageHandler
    {
        private static HttpResponseMessage Respond(HttpRequestMessage request) =>
            request.RequestUri!.AbsolutePath == "/geo.json"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"country\":\"SE\"}", Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NotFound);

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            => Respond(request);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Respond(request));
    }

    private static readonly string _collectorBlock = DomScriptComposer.CollectorBlock(
    [
        ("order", "() => ({ order: (window.__order || []).join(',') })"),
    ]);

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task XhrOnload_FiresAfterTheIssuingScriptFinishes_NotInlineInsideSend(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var renderer = new JsRenderer(
            _fixture.GetFactory(engine),
            new JsRenderOptions { EnableFetch = true },
            NullLogger.Instance,
            _collectorBlock);

        using var client = new HttpClient(new GeoHost());
        var slices = await renderer.CollectAsync(
            Encoding.UTF8.GetBytes(_html), _pageUrl, client, TestContext.Current.CancellationToken);

        var order = Assert.Contains("order", slices).GetProperty("order").GetString();

        // Inline firing would yield "onload,threw:...,after-send" — the handler running before __lateInit exists.
        Assert.Equal("after-send,onload,lateInit-ran", order);
    }
}
