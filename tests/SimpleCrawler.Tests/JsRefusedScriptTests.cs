using Microsoft.Extensions.Logging;
using SimpleCrawler.Js.Models;
using SimpleCrawler.Tests.Fixtures;
using SimpleCrawler.Tests.Helpers;
using SimpleCrawler.Tests.Models;
using System.Net;
using System.Text;

namespace SimpleCrawler.Tests;

/// <summary>
/// A <c>src</c> the host asked for and was refused must be reported. The globals that script would have
/// registered are absent whichever way the fetch failed, and a consumer that reads absence as a finding tells
/// a partial render from a page that never carried the code only by the renderer's warnings — a refusal that
/// logs nothing is that consumer's silent zero. Both the classic-script and the module path are asserted: each
/// fetches through its own code, and the module path answers a refusal with an empty module.
/// <para>
/// Both engines are asserted because the invariant belongs to the renderer rather than to a backend.
/// </para>
/// </summary>
[Collection("Crawler")]
public class JsRefusedScriptTests : JsDomRendererTestBase, IClassFixture<JsRendererFixture>
{
    public JsRefusedScriptTests(JsRendererFixture fixture) : base(fixture)
    {
    }

    private const string _html = """
        <!doctype html><html><head>
        <script src="https://www.example.test/gone.js"></script>
        <script type="module" src="https://www.example.test/gone.mjs"></script>
        </head><body><div id="t"></div>
        <script>document.getElementById('t').setAttribute('data-after', '1');</script>
        </body></html>
        """;

    private sealed class RefusingHost : HttpMessageHandler
    {
        private static HttpResponseMessage Respond() => new(HttpStatusCode.NotFound);

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            => Respond();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Respond());
    }

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task ARefusedScript_IsReportedAndCostsOnlyItself(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var logger = new CountingLogger();
        var renderer = CreateJsRenderer(engine, logger: logger);

        using var client = new HttpClient(new RefusingHost());
        var result = await renderer.RenderAsync(
            Encoding.UTF8.GetBytes(_html), "https://www.example.test/", client, TestContext.Current.CancellationToken);

        // The attribute form, not the bare string: the rendered output echoes the script source verbatim, so
        // only the serialized attribute proves the script after the refused ones actually ran.
        Assert.Contains("data-after=\"1\"", Encoding.UTF8.GetString(result));

        Assert.Contains(logger.Warnings, m => m.Contains("gone.js") && m.Contains("404"));
        Assert.Contains(logger.Warnings, m => m.Contains("gone.mjs") && m.Contains("404"));
    }

    private sealed class CountingLogger : ILogger
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
                Warnings.Add(formatter(state, exception));
        }
    }
}
