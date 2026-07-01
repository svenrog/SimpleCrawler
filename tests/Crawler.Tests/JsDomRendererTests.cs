using Crawler.Core;
using Crawler.Js.Abstractions;
using Crawler.Js.AngleSharp;
using Crawler.Js.HtmlAgilityPack;
using Crawler.Js.Jint;
using Crawler.Js.Models;
using Crawler.Js.Parsing;
using Crawler.Js.Rendering;
using Crawler.Js.V8;
using Crawler.Tests.Helpers;
using Crawler.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;

namespace Crawler.Tests;

// Exercises the pure-JS DOM path (dom.js) without a host: dom.js parses the shell, the inline
// script mutates the JS DOM, and the tree is serialized back to HTML — no managed DOM wrappers involved.
// Theory'd over both engines since the JS DOM is the single code path for Jint + V8.
public class JsDomRendererTests
{
    private static JsRenderer CreateJsRenderer(JsEngine engine, JsRenderOptions? options = null, ILogger? logger = null, IHtmlParser? htmlParser = null)
    {
        var services = new ServiceCollection();
        var key = engine == JsEngine.V8 ? "js-v8" : "js-jint";
        if (engine == JsEngine.V8)
            services.AddV8Crawler(new CrawlerOptions());
        else
            services.AddJintCrawler(new CrawlerOptions());
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredKeyedService<IJsEngineFactory>(key);
        return new JsRenderer(factory, options ?? new JsRenderOptions(), htmlParser, logger ?? NullLogger.Instance);
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

    // innerHTML must materialise real child nodes, not just cache a string for serialisation: cloneNode,
    // lastChild, querySelector and the link collector all walk childNodes, so a lazy setter hides
    // innerHTML-injected anchors (and crashes jQuery's `cloneNode(true).lastChild.defaultValue` probe).
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_InnerHtmlPopulatesLiveDom(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="r"></div>
            <script>
            var r = document.getElementById('r');
            r.innerHTML = '<a href="/inner-a">a</a><span class="s">x</span>';
            var anchors = r.querySelectorAll('a').length;
            var lastName = r.lastChild.nodeName;
            var clonedLast = r.cloneNode(true).lastChild.nodeName;
            var out = document.createElement('a');
            out.setAttribute('href', '/probe?anchors=' + anchors + '&last=' + lastName + '&clone=' + clonedLast);
            document.body.appendChild(out);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/inner-a\"", rendered);
        Assert.Contains("anchors=1", rendered);
        Assert.Contains("last=SPAN", rendered);
        Assert.Contains("clone=SPAN", rendered);
    }

    // document.cookie is always a string (bundles call document.cookie.includes(...)), document.location
    // aliases window.location (analytics read document.location.protocol/href), and non-element nodes carry
    // the standard nodeName (#text/#comment) that React hydration lowercases.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_DocumentCookieLocationAndNodeNames(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body>
            <script>
            var before = typeof document.cookie + ':' + document.cookie.includes('x=1');
            document.cookie = 'x=1; path=/';
            var after = document.cookie.includes('x=1');
            var proto = document.location.protocol;
            var hrefMatch = document.location.href === window.location.href;
            var textName = document.createTextNode('t').nodeName;
            var commentName = document.createComment('c').nodeName;
            var out = document.createElement('a');
            out.setAttribute('href', '/probe?before=' + before + '&after=' + after + '&proto=' + proto + '&href=' + hrefMatch + '&text=' + textName + '&comment=' + commentName);
            document.body.appendChild(out);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://example.test/page", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("before=string:false", rendered);
        Assert.Contains("after=true", rendered);
        Assert.Contains("proto=https:", rendered);
        Assert.Contains("href=true", rendered);
        Assert.Contains("text=#text", rendered);
        Assert.Contains("comment=#comment", rendered);
    }

    // Swiper (and most DOM widget libs) probe classList.add/remove/toggle/contains, element.matches/closest
    // and the reflected `dir` property during init; a missing one threw straight into the SPA error boundary
    // dir reflects "" when unset, matches/closest
    // understand class selectors, and classList mutates the live class attribute.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_ClassListMatchesClosestAndDir(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body>
            <div id="wrap" class="outer"><button id="btn" class="a b">x</button></div>
            <script>
            var wrap = document.getElementById('wrap');
            var btn = document.getElementById('btn');
            var dir = typeof wrap.dir + ':' + (wrap.dir === '');
            btn.classList.add('c');
            btn.classList.remove('a');
            var toggled = btn.classList.toggle('d') + '/' + btn.classList.toggle('b');
            var cls = btn.getAttribute('class');
            var has = btn.classList.contains('c');
            var selfMatch = btn.matches('.c') + '/' + btn.matches('button.c') + '/' + btn.matches('.a');
            var closestId = btn.closest('.outer').id;
            var out = document.createElement('a');
            out.setAttribute('href', '/probe?dir=' + dir + '&cls=' + cls + '&toggled=' + toggled + '&has=' + has + '&match=' + selfMatch + '&closest=' + closestId);
            document.body.appendChild(out);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://example.test/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("dir=string:true", rendered);
        Assert.Contains("cls=c d", rendered);
        Assert.Contains("toggled=true/false", rendered);
        Assert.Contains("has=true", rendered);
        Assert.Contains("match=true/true/false", rendered);
        Assert.Contains("closest=wrap", rendered);
    }

    // <select> exposes an options collection (its <option> descendants) and each option reflects its value
    // (the `value` attribute, else the text) — react-dom's updateOptions does `node.options` then iterates
    // `.length`/`.value` to sync selection, so a missing options collection read undefined and aborted the
    // whole hydration render. Both the parsed SSR shell and createElement('select') must carry it.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_SelectExposesOptionsCollectionAndValues(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body>
            <select id="s"><option value="a">A</option><option value="b">B</option><option>C</option></select>
            <script>
            var s = document.getElementById('s');
            var opts = s.options;
            var values = [];
            for (var i = 0; i < opts.length; i++) values.push(opts[i].value);
            opts[1].selected = true;
            var created = document.createElement('select');
            var out = document.createElement('a');
            out.setAttribute('href', '/probe?count=' + opts.length + '&values=' + values.join(',') + '&created=' + created.options.length + '&selected=' + opts[1].selected);
            document.body.appendChild(out);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("count=3", rendered);
        Assert.Contains("values=a,b,C", rendered);
        Assert.Contains("created=0", rendered);
        Assert.Contains("selected=true", rendered);
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

    // Node load/error events on runtime-appended resources — the webpack lazy-chunk mechanism React Router
    // code-splitting depends on. A same-origin <script src> is fetched and executed by the resource drain,
    // then its load event fires (the bundle's own anchor proves execution, the onload handler's proves the
    // event); a missing src fires error instead; an appended <link> fires load without a fetch. Regressed
    // when the pure-JS DOM dropped the Bridge's resource drain, leaving SPAs stuck on "Loading chunk failed".
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_AppendedResourceNodes_FireLoadAndErrorEvents(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            var d = document.getElementById('t');
            var ok = document.createElement('script');
            ok.src = '/chunk.js';
            ok.onload = function () { var a = document.createElement('a'); a.setAttribute('href', '/loaded'); d.appendChild(a); };
            ok.onerror = function () { var a = document.createElement('a'); a.setAttribute('href', '/notthis'); d.appendChild(a); };
            document.head.appendChild(ok);
            var bad = document.createElement('script');
            bad.src = '/missing.js';
            bad.onerror = function () { var a = document.createElement('a'); a.setAttribute('href', '/errored'); d.appendChild(a); };
            document.head.appendChild(bad);
            var css = document.createElement('link');
            css.rel = 'stylesheet';
            css.href = '/style.css';
            css.addEventListener('load', function () { var a = document.createElement('a'); a.setAttribute('href', '/css'); d.appendChild(a); });
            document.head.appendChild(css);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        using var client = new HttpClient(new StubResourceHandler());
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", client, CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/executed\"", rendered);
        Assert.Contains("href=\"/loaded\"", rendered);
        Assert.Contains("href=\"/errored\"", rendered);
        Assert.Contains("href=\"/css\"", rendered);
        Assert.DoesNotContain("href=\"/notthis\"", rendered);
    }

    // Viewport: the JS DOM reports the configured screen size (default desktop 1920x1080) through
    // window.innerWidth/screen/documentElement.clientWidth, and matchMedia evaluates width queries against it
    // — so a responsive bundle takes its desktop branch. A mobile override flips every signal, proving both
    // the media-query evaluator and the JsRenderOptions.Viewport plumbing. Was an always-false matchMedia stub.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_SimulatesConfiguredViewport(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            var d = document.getElementById('t');
            var desktop = matchMedia('(min-width: 1024px)').matches && !matchMedia('(max-width: 768px)').matches;
            var a = document.createElement('a');
            a.setAttribute('href', '/w' + window.innerWidth + 'x' + window.innerHeight + '-screen' + screen.width + '-client' + document.documentElement.clientWidth + '-' + (desktop ? 'desktop' : 'mobile'));
            d.appendChild(a);
            </script>
            </body></html>
            """;

        var desktop = await RenderViewport(engine, html, null);
        Assert.Contains("href=\"/w1920x1080-screen1920-client1920-desktop\"", desktop);

        var mobileOptions = new JsRenderOptions { Viewport = new Viewport { Width = 375, Height = 812 } };
        var mobile = await RenderViewport(engine, html, mobileOptions);
        Assert.Contains("href=\"/w375x812-screen375-client375-mobile\"", mobile);
    }

    private static async Task<string> RenderViewport(JsEngine engine, string html, JsRenderOptions? options)
    {
        var renderer = CreateJsRenderer(engine, options);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        return Encoding.UTF8.GetString(result);
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
            </script>
            </body></html>
            """;

        var enabled = new CapturingLogger();
        await CreateJsRenderer(engine, new JsRenderOptions { ScriptLogging = LogLevel.Information }, enabled)
            .RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);

        Assert.Contains((LogLevel.Information, "hello world number 42"), enabled.Entries);
        Assert.Contains((LogLevel.Error, "boom"), enabled.Entries);
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

    private sealed class StubResourceHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/chunk.js")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("var x=document.createElement('a');x.setAttribute('href','/executed');document.getElementById('t').appendChild(x);"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    // document.currentScript must be the executing <script> while a classic script runs (so webpack's
    // auto-public-path, and Next's `instanceof HTMLScriptElement` invariant over it, sees a real script
    // element instead of undefined) and back to null once it returns. The drained chunk reads it during its
    // own execution and reports its src — the exact path that threw Next's InvariantError.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_CurrentScript_IsExecutingScript_ThenNull(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            var ics = document.currentScript;
            var d = document.getElementById('t');
            var a = document.createElement('a');
            a.setAttribute('href', '/inline-' + (ics instanceof HTMLScriptElement) + '-src' + ics.src);
            d.appendChild(a);
            var chunk = document.createElement('script');
            chunk.src = '/cs-chunk.js';
            document.head.appendChild(chunk);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        using var client = new HttpClient(new CurrentScriptHandler());
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", client, CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/inline-true-src\"", rendered);
        Assert.Contains("href=\"/chunk-true-/cs-chunk.js-after-null\"", rendered);
    }

    private sealed class CurrentScriptHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/cs-chunk.js")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "var cs=document.currentScript;" +
                        "var ok=(cs instanceof HTMLScriptElement)&&/cs-chunk\\.js$/.test(cs.src);" +
                        "setTimeout(function(){" +
                        "var after=document.currentScript===null?'null':'set';" +
                        "var a=document.createElement('a');" +
                        "a.setAttribute('href','/chunk-'+ok+'-'+cs.src.replace(/^https?:\\/\\/[^/]+/,'')+'-after-'+after);" +
                        "document.getElementById('t').appendChild(a);},0);"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    // Lazy-mount-on-visible blocks (e.g. AntD skeletons) stay placeholders until an IntersectionObserver
    // reports them intersecting; the headless render has no scroll, so observe() must fire isIntersecting once.
    // The injected content goes through createRange().createContextualFragment() — the script-injection path
    // real bundles use — so this also guards Range and DocumentFragment.querySelectorAll.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_IntersectionObserver_MountsLazyContentViaContextualFragment(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head><title>t</title></head>
            <body><div id="slot"></div>
            <script>
                var slot = document.getElementById('slot');
                var io = new IntersectionObserver(function (entries) {
                    for (var i = 0; i < entries.length; i++) {
                        if (!entries[i].isIntersecting) continue;
                        var frag = document.createRange().createContextualFragment('<a href="/lazy-loaded">go</a><span class="block">x</span>');
                        if (frag.querySelectorAll('a').length) entries[i].target.appendChild(frag);
                    }
                });
                io.observe(slot);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/lazy-loaded\"", rendered);
        Assert.Contains("class=\"block\"", rendered);
    }

    // CharacterData/DocumentType globals: consent/IAB polyfills eagerly
    // build [Element.prototype, CharacterData.prototype, DocumentType.prototype] to patch EventTarget onto
    // every node base — a missing global threw ReferenceError straight into the SPA error boundary. Text/Comment
    // also extend CharacterData for real, so instanceof and any prototype patch reach them; DocumentType exists
    // as a patchable global even though the parser emits no doctype node.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_CharacterDataAndDocumentTypeGlobalsAreDefined(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            var bases = [Element.prototype, CharacterData.prototype, DocumentType.prototype];
            CharacterData.prototype.__crawlMark = 'cd';
            var textIsCd = document.createTextNode('x') instanceof CharacterData;
            var commentIsCd = document.createComment('y') instanceof CharacterData;
            var inherited = document.createTextNode('x').__crawlMark === 'cd';
            var a = document.createElement('a');
            a.setAttribute('href', '/ok-' + bases.length + '-' + textIsCd + '-' + commentIsCd + '-' + inherited);
            document.getElementById('t').appendChild(a);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/ok-3-true-true-true\"", rendered);
    }

    // A native parser (AngleSharp/HAP) feeds the initial DOM via __crawlerLoadTree; the parsed anchors,
    // canonical/robots head tags, and the inline-script mutation must all match the JS tokenizer path, so a
    // crawl extracts the same links regardless of which parser is registered.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_NativeParser_ProducesSameDomAsJsParser(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head><link rel="canonical" href="/canon" /><meta name="robots" content="noindex" /></head>
            <body>
            <a href="/one">1</a><a href="/two">2</a><a href="/three">3</a>
            <div id="t"></div>
            <script>var a=document.createElement('a');a.setAttribute('href','/injected');document.getElementById('t').appendChild(a);</script>
            </body></html>
            """;

        var js = await RenderHtml(engine, html, null);
        var angleSharp = await RenderHtml(engine, html, new AngleSharpHtmlParser());
        var hap = await RenderHtml(engine, html, new HtmlAgilityPackHtmlParser());

        foreach (var renderer in new[] { js, angleSharp, hap })
        {
            Assert.Contains("href=\"/one\"", renderer);
            Assert.Contains("href=\"/two\"", renderer);
            Assert.Contains("href=\"/three\"", renderer);
            Assert.Contains("href=\"/injected\"", renderer);
            Assert.Contains("href=\"/canon\"", renderer);
        }
    }

    private static async Task<string> RenderHtml(JsEngine engine, string html, IHtmlParser? parser)
    {
        var renderer = CreateJsRenderer(engine, htmlParser: parser);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        return Encoding.UTF8.GetString(result);
    }
}
