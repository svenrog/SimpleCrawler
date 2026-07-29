using Microsoft.Extensions.Logging.Abstractions;
using SimpleCrawler.Js.Models;
using SimpleCrawler.Js.Rendering;
using SimpleCrawler.Tests.Fixtures;
using SimpleCrawler.Tests.Helpers;
using SimpleCrawler.Tests.Models;
using System.Text;

namespace SimpleCrawler.Tests;

/// <summary>
/// A page whose script recurses without end must cost that script and nothing more. Interpreted JS runs on
/// the CLR stack, so the failure this guards is not a wrong result but a <see cref="StackOverflowException"/>
/// — which .NET gives a host no way to catch: the process dies mid-crawl, taking every page already rendered
/// with it. Observed on live sites, and browsers answer the same code with a catchable error, so surviving it
/// is the baseline rather than a nicety.
/// <para>
/// Both engines are asserted because the invariant belongs to the renderer, not to Jint: V8 raises a JS
/// <c>RangeError</c> the page can catch, Jint stops the recursion at the host limit, and either way the
/// scripts after it still run.
/// </para>
/// </summary>
[Collection("Crawler")]
public class JsRunawayRecursionTests : JsDomRendererTestBase, IClassFixture<JsRendererFixture>
{
    public JsRunawayRecursionTests(JsRendererFixture fixture) : base(fixture)
    {
    }

    /// <summary>
    /// Mutual recursion, not self-recursion: a host limit that counts one function's own repetitions is
    /// reached later this way, and a runaway that cycles through several functions is the shape live pages
    /// actually carry.
    /// </summary>
    private const string _html = """
        <html><body><div id="t"></div>
        <script>
        function ping(n) { return pong(n + 1); }
        function pong(n) { return ping(n + 1); }
        try { ping(0); } catch (e) { document.getElementById('t').setAttribute('data-caught', '1'); }
        </script>
        <script>document.getElementById('t').setAttribute('data-after', '1');</script>
        </body></html>
        """;

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task ARunawayRecursion_CostsItsOwnScriptAndNotTheRender(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        using var client = new HttpClient();
        var renderer = CreateJsRenderer(engine);

        var result = await renderer.RenderAsync(
            Encoding.UTF8.GetBytes(_html), "http://localhost:5000/", client, TestContext.Current.CancellationToken);

        var rendered = Encoding.UTF8.GetString(result);

        // The attribute form, not the bare string: the rendered output echoes the script source verbatim, so
        // only the serialized attribute proves the script after the runaway one actually ran.
        Assert.Contains("data-after=\"1\"", rendered);
    }
}
