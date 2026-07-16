using Microsoft.Extensions.Logging;
using SimpleCrawler.Js.Models;
using SimpleCrawler.Tests.Fixtures;
using SimpleCrawler.Tests.Helpers;
using SimpleCrawler.Tests.Models;
using System.Text;

namespace SimpleCrawler.Tests;

/// <summary>
/// Globals a third-party SDK reaches for while installing itself (analytics, error reporting, polyfills), and
/// what happens to the render when one of them is missing; also the console-to-ILogger bridge such SDKs log through.
/// </summary>
[Collection("Crawler")]
public class JsDomRendererSdkGlobalsTests : JsDomRendererTestBase, IClassFixture<JsRendererFixture>
{
    public JsDomRendererSdkGlobalsTests(JsRendererFixture fixture) : base(fixture)
    {
    }

    // Web APIs (custom elements, structuredClone, AbortController-shaped code) throw DOMException; a bundle that
    // references the constructor for `new DOMException`, `instanceof`, or subclassing must find it as a global.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_DOMExceptionConstructorAndProperties(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            var e = new DOMException('nope', 'NotFoundError');
            var ok = e instanceof Error && e.name === 'NotFoundError' && e.message === 'nope'
                && e.code === 8 && DOMException.NOT_FOUND_ERR === 8;
            var thrown = '';
            try { throw new DOMException('stop', 'AbortError'); } catch (x) { thrown = x.name + x.code; }
            var a = document.createElement('a');
            a.setAttribute('href', '/' + (ok ? 'ok' : 'bad') + '-' + thrown);
            document.getElementById('t').appendChild(a);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/ok-AbortError20\"", rendered);
    }

    // core-js's Promise feature-detection forces its own (potentially finally-less) Promise polyfill unless
    // window.PromiseRejectionEvent is a callable global; a bundle then hits `promise.finally is not a function`
    // and its hydration dies. The global must exist, be callable, and carry promise/reason as an Event.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_PromiseRejectionEvent_IsCallableEventGlobal(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head><body>
            <script>
            try {
              var p = Promise.resolve();
              var ev = new PromiseRejectionEvent('unhandledrejection', { promise: p, reason: 'boom' });
              var ok = typeof PromiseRejectionEvent === 'function' && ev instanceof Event &&
                       ev.type === 'unhandledrejection' && ev.promise === p && ev.reason === 'boom';
              var l = document.createElement('a'); l.setAttribute('href', ok ? '/ok' : '/err');
              document.body.appendChild(l);
            } catch (err) {
              var e = document.createElement('a'); e.setAttribute('href', '/err'); document.body.appendChild(e);
            }
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.DoesNotContain("href=\"/err\"", rendered);
        Assert.Contains("href=\"/ok\"", rendered);
    }

    // The globals an analytics/tracing SDK reaches for while installing itself. Each is constructed or called
    // during init, so a missing one throws a ReferenceError *through* that init and the SDK sets none of the
    // globals it would have — the page still renders, nothing surfaces, and the technology reads as absent.
    // PerformanceObserver never fires (a layout-less render produces no timing entries, the same reason
    // performance.getEntries() is empty) but must exist and accept observe(); Worker is constructed bare in
    // real bundles, so it must not throw; sendBeacon must report success, because the default no-fetch posture
    // exists precisely so a bundle runs while its beacon goes nowhere.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_SdkInitGlobals_ExistAndAreInert(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><body>
            <script>
              var fired = false;
              var po = new PerformanceObserver(function () { fired = true; });
              po.observe({ type: 'largest-contentful-paint', buffered: true });
              var lcp = PerformanceObserver.supportedEntryTypes.indexOf('largest-contentful-paint') >= 0;
              var records = po.takeRecords().length;
              po.disconnect();
              var w = new Worker('/worker.js');
              w.postMessage({ hello: 1 });
              w.terminate();
              var beacon = navigator.sendBeacon('/collect', 'x');
              var a = document.createElement('a');
              a.setAttribute('href', '/ok-' + lcp + '-' + fired + '-' + records + '-' + beacon);
              document.body.appendChild(a);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://example.test/", new HttpClient(), CancellationToken.None);

        // supported, never fired, no records, beacon accepted — and the subtree below the SDK init survived.
        Assert.Contains("href=\"/ok-true-false-0-true\"", Encoding.UTF8.GetString(result));
    }

    // The counterpart to the above: what a *missing* SDK-init global costs. The throw lands inside the init,
    // so everything the SDK would have built — here the link standing in for its globals — is silently gone.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_SdkInitGlobals_AreReachedBeforeTheSdkSetsAnything(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><body>
            <script>
              (function initSdk() {
                new PerformanceObserver(function () { }).observe({ type: 'paint' });
                window.__sdk = 'installed';
                var a = document.createElement('a');
                a.setAttribute('href', '/sdk-installed');
                document.body.appendChild(a);
              })();
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://example.test/", new HttpClient(), CancellationToken.None);

        Assert.Contains("href=\"/sdk-installed\"", Encoding.UTF8.GetString(result));
    }

    // console: the bundle's console.* calls reach the host ILogger only when JsRenderOptions.ScriptLogging
    // opts in, formatting (incl. %-substitution) happens JS-side, and the configured level acts as a floor —
    // console.debug is dropped under an Information floor while console.error gets through. Regressed when the
    // big JS refactor removed LoggingJsConsole, leaving ScriptLogging dead and console a no-op.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_ConsoleRoutesToLogger_GatedByScriptLogging(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            console.debug('debug %s', 'noise');
            console.log('hello %s number %d', 'world', 42);
            console.error('boom');
            console.error(new Error('kaboom'));
            </script>
            </body></html>
            """;

        var enabled = new CapturingLogger();
        await CreateJsRenderer(engine, new JsRenderOptions { ScriptLogging = LogLevel.Information }, enabled)
            .RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);

        Assert.Contains((LogLevel.Information, "hello world number 42"), enabled.Entries);
        Assert.Contains((LogLevel.Error, "boom"), enabled.Entries);
        Assert.Contains(enabled.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("kaboom"));
        Assert.DoesNotContain(enabled.Entries, e => e.Level == LogLevel.Error && e.Message == "{}");
        Assert.DoesNotContain(enabled.Entries, e => e.Level == LogLevel.Debug);

        var suppressed = new CapturingLogger();
        await CreateJsRenderer(engine, new JsRenderOptions(), suppressed)
            .RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);

        Assert.Empty(suppressed.Entries);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
