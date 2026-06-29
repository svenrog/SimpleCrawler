using Crawler.Js.Abstractions;
using Crawler.Js.Jint;
using Crawler.Js.Models;
using Crawler.Js.Rendering;
using Crawler.Js.V8;
using Crawler.Core;
using Crawler.Tests.Helpers;
using Crawler.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace Crawler.Tests;

// Exercises the pure-JS DOM path (dom.js) without a host: dom.js parses the shell, the inline
// script mutates the JS DOM, and the tree is serialized back to HTML — no managed DOM wrappers involved.
// Theory'd over both engines since the JS DOM is the single code path for Jint + V8.
public class JsDomRendererTests
{
    private static JsRenderer CreateJsRenderer(JsEngine engine)
    {
        var services = new ServiceCollection();
        var key = engine == JsEngine.V8 ? "js-v8" : "js-jint";
        if (engine == JsEngine.V8)
            services.AddV8Crawler(new CrawlerOptions());
        else
            services.AddJintCrawler(new CrawlerOptions());
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredKeyedService<IJsEngineFactory>(key);
        return new JsRenderer(factory, new JsRenderOptions(), NullLogger.Instance);
    }

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_ParsesShell_RunsInlineScript_AndSerializesMutation(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head><title>t</title></head>
            <body><a href="/static">s</a><div id="r"></div>
            <script>var a=document.createElement('a');a.setAttribute('href','/injected');a.textContent='i';document.body.appendChild(a);</script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("<title>t</title>", rendered);
        Assert.Contains("href=\"/static\"", rendered);
        Assert.Contains("href=\"/injected\"", rendered);
    }

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_RendersInnerHtmlInjection(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="r"></div>
            <script>document.getElementById('r').innerHTML='<a href="/inner">i</a>';</script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/inner\"", rendered);
    }

    // customElements: a parsed <my-widget> is upgraded retroactively when the bundle defines it (the Astro
    // island path), and a createElement'd instance fires connectedCallback on attach. Both paths must
    // hydrate the anchor the connected callback injects.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_UpgradeCustomElement_AndFireConnectedCallback(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body>
            <my-widget data-src="/upgraded">seed</my-widget>
            <div id="host"></div>
            <script>
            class MyWidget extends HTMLElement {
              connectedCallback() {
                var a = document.createElement('a');
                a.setAttribute('href', this.getAttribute('data-src'));
                a.textContent = 'w';
                this.appendChild(a);
              }
            }
            customElements.define('my-widget', MyWidget);
            var fresh = document.createElement('my-widget');
            fresh.setAttribute('data-src', '/created');
            document.getElementById('host').appendChild(fresh);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/upgraded\"", rendered);
        Assert.Contains("href=\"/created\"", rendered);
    }

    // Event/CustomEvent: a CustomEvent carries detail from its init, and a real dispatch fires a listener
    // registered on an element (the Element.dispatchEvent upgrade), both hydrating their anchors.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_CustomEventDetailAndElementDispatch(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            var d = document.getElementById('t');
            var a1 = document.createElement('a'); a1.setAttribute('href', new CustomEvent('x', { detail: '/detail' }).detail); a1.textContent = 'e'; d.appendChild(a1);
            var a2 = document.createElement('a'); a2.textContent = 'd'; d.appendChild(a2);
            a2.addEventListener('c', function () { a2.setAttribute('href', '/dispatched'); });
            a2.dispatchEvent(new Event('c'));
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/detail\"", rendered);
        Assert.Contains("href=\"/dispatched\"", rendered);
    }

    // TextEncoder/TextDecoder: a UTF-8 round-trip survives both directions.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_TextEncoderDecoderRoundTrip(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            var back = new TextDecoder().decode(new TextEncoder().encode('/td'));
            var ok = new TextEncoder().encode('AB').length === 2 && back === '/td';
            var a = document.createElement('a'); a.setAttribute('href', ok ? '/td' : '/fail'); a.textContent = 't';
            document.getElementById('t').appendChild(a);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/td\"", rendered);
    }

    // crypto: getRandomValues fills and returns the buffer, randomUUID is a v4 UUID (version nibble '4').
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_CryptoRandomValuesAndUuid(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            var u = crypto.randomUUID();
            var ok = crypto.getRandomValues(new Uint8Array(4)).length === 4 && u.length === 36 && u.charAt(8) === '-' && u.charAt(14) === '4';
            var a = document.createElement('a'); a.setAttribute('href', ok ? '/crypto' : '/fail'); a.textContent = 'c';
            document.getElementById('t').appendChild(a);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/crypto\"", rendered);
    }

    // MessageChannel: port2.postMessage schedules port1.onmessage as a macrotask on the unified task queue,
    // which the drain pumps — so the anchor lands only after the drain settles.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_MessageChannelDeliversViaDrain(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            var ch = new MessageChannel();
            ch.port1.onmessage = function (ev) {
              var a = document.createElement('a'); a.setAttribute('href', ev.data); a.textContent = 'm';
              document.getElementById('t').appendChild(a);
            };
            ch.port2.postMessage('/mc');
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/mc\"", rendered);
    }

    // localStorage/sessionStorage: separate in-memory stores, items survive a set→get round-trip.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_StorageRoundTrip(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            localStorage.setItem('a', '/stored');
            sessionStorage.setItem('b', '/sess');
            var d = document.getElementById('t');
            var a1 = document.createElement('a'); a1.setAttribute('href', localStorage.getItem('a')); a1.textContent = 'l'; d.appendChild(a1);
            var a2 = document.createElement('a'); a2.setAttribute('href', sessionStorage.getItem('b')); a2.textContent = 's'; d.appendChild(a2);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/stored\"", rendered);
        Assert.Contains("href=\"/sess\"", rendered);
    }

    // performance: now() is a non-negative number and timeOrigin is numeric.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_PerformanceNow(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            var n = performance.now();
            var ok = typeof n === 'number' && n >= 0 && typeof performance.timeOrigin === 'number';
            var a = document.createElement('a'); a.setAttribute('href', ok ? '/perf' : '/fail'); a.textContent = 'p';
            document.getElementById('t').appendChild(a);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/perf\"", rendered);
    }
}
