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
/// Pins <c>Function.prototype.toString()</c> against the source a bundle actually shipped, for an external
/// script as well as an inline one. The two arrive by different routes — an external script is parsed
/// outside the engine as a cached prepared script, which does not inherit the engine's source-text retention
/// — so retaining it for one and not the other is invisible until a bundle reads its own source.
/// <para>
/// A tripwire rather than a unit test because of how the failure presents: an obfuscator's self-defence
/// check compares a function against the text it expects, and a <c>[native code]</c> placeholder fails that
/// comparison, sending the bundle down its tampered-with branch. One such payload answers by entering a loop
/// that appends to an array it is iterating — no throw, no diagnostic, memory to exhaustion.
/// </para>
/// </summary>
[Collection("Crawler")]
public class JsFunctionSourceTextTests : IClassFixture<JsRendererFixture>
{
    private readonly JsRendererFixture _fixture;

    public JsFunctionSourceTextTests(JsRendererFixture fixture)
    {
        _fixture = fixture;
    }

    private const string _pageUrl = "https://www.example.test/";

    private const string _html = """
        <!doctype html><html><head></head><body>
        <script src="/bundle.js"></script>
        <script>window.__inline = (function () { return 'dev'; }).toString();</script>
        </body></html>
        """;

    /// <summary>The minified shape an obfuscated payload defines its guarded function in.</summary>
    private const string _bundle = """
        window.__external = {'removeCookie':function(){return'dev';}}.removeCookie.toString();
        """;

    private sealed class ScriptHost : HttpMessageHandler
    {
        private static HttpResponseMessage Respond(HttpRequestMessage request) =>
            request.RequestUri!.AbsolutePath == "/bundle.js"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_bundle, Encoding.UTF8, "application/javascript"),
                }
                : new HttpResponseMessage(HttpStatusCode.NotFound);

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            => Respond(request);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Respond(request));
    }

    private static readonly string _collectorBlock = DomScriptComposer.CollectorBlock(
    [
        ("source", """
            () => ({
              external: window.__external || '',
              inline: window.__inline || '',
              // The self-defence test itself, built the way such a payload builds it.
              selfDefence: new RegExp("\\w+ *\\(\\) *{\\w+ *['|\"].+['|\"];? *}").test(window.__external || '')
            })
            """),
    ]);

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task AFunctionFromAnExternalScript_ReportsItsSourceRatherThanANativeCodePlaceholder(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var source = await CollectAsync(engine);

        Assert.Equal("function(){return'dev';}", source.GetProperty("external").GetString());
        Assert.Equal("function () { return 'dev'; }", source.GetProperty("inline").GetString());
        Assert.True(source.GetProperty("selfDefence").GetBoolean());
    }

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

        return Assert.Contains("source", slices);
    }
}
