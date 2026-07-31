using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SimpleCrawler.Js.Abstractions;
using SimpleCrawler.Js.Jint;
using SimpleCrawler.Js.Models;
using SimpleCrawler.Js.Rendering;
using SimpleCrawler.Js.V8;
using SimpleCrawler.Tests.Helpers;
using SimpleCrawler.Tests.Models;
using System.Diagnostics;
using System.Text;

namespace SimpleCrawler.Tests;

/// <summary>
/// Covers the ceiling and the cancellation that are the only things able to stop page JavaScript, which runs
/// synchronously on the calling thread. Without them a page that never returns holds that thread until the
/// process ends: the token handed to the async render method is observed at <c>await</c> points the engine
/// never reaches, and Jint's stack-depth guard bounds recursion rather than time, so a page recursing a few
/// frames a minute while doing exponentially more work per level never approaches it.
/// <para>
/// Both engines are covered because each enforces this with a different mechanism and names its ceiling for
/// what that mechanism can bound — <see cref="JintEngineOptions.ScriptTimeout"/> per execution call,
/// <see cref="V8EngineOptions.PageTimeout"/> per page. Only the observable contract is shared: a spent
/// ceiling is a <see cref="TimeoutException"/>, a cancelled run an <see cref="OperationCanceledException"/>.
/// </para>
/// </summary>
[Collection("Crawler")]
public class JsEngineLimitsTests
{
    // A page that never finishes. Deliberately a flat loop rather than recursion, which would meet the
    // stack-depth guard instead and prove nothing about the ceiling.
    private const string _runaway = """
        <!doctype html><html><body>
        <script>
          while (true) { Math.sqrt(Math.random()); }
        </script>
        </body></html>
        """;

    // Loose enough that a slow agent cannot fail these, tight enough that the limit under test must be the
    // one that stopped it: a setting that failed to reach the engine leaves the 30s default, outside this.
    private static readonly TimeSpan _stoppedByTheLimitUnderTest = TimeSpan.FromSeconds(20);

    private static byte[] Runaway => Encoding.UTF8.GetBytes(_runaway);

    // Each engine's ceiling lives on its own options, so a test that sets one builds its own container
    // rather than sharing the fixture's default-configured factories. Both configure the standard way;
    // that this reaches V8 at all is what V8EngineOptionsTests pins.
    private static ServiceProvider Provider(JsEngine engine, TimeSpan timeout)
    {
        var services = new ServiceCollection();
        if (engine == JsEngine.V8)
        {
            services.AddV8JsEngine();
            services.Configure<V8EngineOptions>(o => o.PageTimeout = timeout);
        }
        else
        {
            services.AddJintJsEngine();
            services.Configure<JintEngineOptions>(o => o.ScriptTimeout = timeout);
        }

        return services.BuildServiceProvider();
    }

    private static JsRenderer Renderer(ServiceProvider provider) =>
        new(provider.GetRequiredService<IJsEngineFactory>(), new JsRenderOptions(), NullLogger.Instance);

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task A_page_that_never_finishes_is_abandoned_when_its_ceiling_is_spent(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        using var provider = Provider(engine, TimeSpan.FromSeconds(2));
        var elapsed = Stopwatch.StartNew();

        await Assert.ThrowsAsync<TimeoutException>(() => Renderer(provider).RenderAsync(
            Runaway, "http://localhost:5000/", new HttpClient(), TestContext.Current.CancellationToken));

        Assert.True(elapsed.Elapsed < _stoppedByTheLimitUnderTest, $"took {elapsed.Elapsed}");
    }

    // The cancellation has to reach the engine itself. A token observed only at the renderer's await points
    // leaves a running page untouched, which is what makes an operator's cancel a request the run ignores.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task A_running_page_is_stopped_by_the_callers_token(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        using var provider = Provider(engine, TimeSpan.Zero);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var elapsed = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Renderer(provider).RenderAsync(
            Runaway, "http://localhost:5000/", new HttpClient(), cancellation.Token));

        Assert.True(elapsed.Elapsed < _stoppedByTheLimitUnderTest, $"took {elapsed.Elapsed}");
    }

    // A cancelled run must not read as a failed render: Jint reports it as ExecutionCanceledException, which
    // derives from its own base rather than from OperationCanceledException, so a caller that treats any
    // other exception as "this page failed" would swallow the cancellation and carry on.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task A_cancelled_page_is_not_reported_as_a_failed_render(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        using var provider = Provider(engine, TimeSpan.Zero);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var thrown = await Record.ExceptionAsync(() => Renderer(provider).RenderAsync(
            Runaway, "http://localhost:5000/", new HttpClient(), cancellation.Token));

        Assert.IsAssignableFrom<OperationCanceledException>(thrown);
    }

    // An ordinary page is untouched by a ceiling it never comes close to spending.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task A_settling_page_renders_unaffected(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><body>
            <script>document.body.setAttribute('data-ran', '1');</script>
            </body></html>
            """;

        using var provider = Provider(engine, TimeSpan.FromSeconds(30));

        var rendered = await Renderer(provider).RenderAsync(
            Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), TestContext.Current.CancellationToken);

        Assert.Contains("data-ran", Encoding.UTF8.GetString(rendered), StringComparison.Ordinal);
    }
}
