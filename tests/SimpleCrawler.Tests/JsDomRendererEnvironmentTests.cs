using SimpleCrawler.Js.Models;
using SimpleCrawler.Tests.Fixtures;
using SimpleCrawler.Tests.Helpers;
using SimpleCrawler.Tests.Models;
using System.Text;

namespace SimpleCrawler.Tests;

/// <summary>
/// document/window environment: cookies, location, style, viewport, observers, and the event/timer queue.
/// </summary>
[Collection("Crawler")]
public class JsDomRendererEnvironmentTests : JsDomRendererTestBase, IClassFixture<JsRendererFixture>
{
    public JsDomRendererEnvironmentTests(JsRendererFixture fixture) : base(fixture)
    {
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

    // document.domain mirrors the page origin's hostname; behaviors code reads/splits it and throws its own
    // "Unable to get document domain" when it's undefined. Assignment (legacy relaxation) is a tolerated no-op.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_DocumentDomain_MirrorsLocationHostname(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            document.domain = 'example.test';
            var a = document.createElement('a');
            a.setAttribute('href', '/' + document.domain);
            document.getElementById('t').appendChild(a);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://example.test/page", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/example.test\"", rendered);
    }

    // `lang` is a reflected attribute on every element; `document.documentElement.lang` must be a string ("" when
    // unset), never undefined. i18n/consent code reads it as a string and calls string methods on it directly —
    // a consent SDK does `document.documentElement.lang.replace(/_/, "-")` to pick its banner language — so an
    // undefined `.lang` is a TypeError. Here that throw would land in an un-awaited init promise and be dropped,
    // stalling the SDK with nothing in the render output to show for it; assert the reflection at this seam.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_DocumentElementLang_ReflectsAttributeAsString(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html lang="en"><body><div id="t"></div>
            <script>
            try {
              var langed = document.documentElement.lang;                 // "en", set on <html>
              var transformed = langed.replace(/_/, '-').toLowerCase();   // must not throw on undefined
              var unset = document.createElement('div').lang;             // "" when unset, never undefined
              var ok = langed === 'en' && transformed === 'en' && unset === '';
              var a = document.createElement('a'); a.setAttribute('href', ok ? '/ok' : '/bad');
              document.getElementById('t').appendChild(a);
            } catch (err) {
              var b = document.createElement('a'); b.setAttribute('href', '/err'); document.getElementById('t').appendChild(b);
            }
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.DoesNotContain("href=\"/err\"", rendered);
        Assert.DoesNotContain("href=\"/bad\"", rendered);
        Assert.Contains("href=\"/ok\"", rendered);
    }

    // getComputedStyle must return a declaration whose every property reads back "" (never null/undefined),
    // accessed both by name (getPropertyValue) and as a direct property. Elementor's getCurrentDeviceMode does
    // `getComputedStyle(el, ':after').content.replace(...)`, so a `.content` that comes back undefined throws
    // "Cannot read properties of undefined (reading 'replace')" and aborts frontend init inside the drain.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_GetComputedStyle_ReturnsEmptyStringsForAnyProperty(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            try {
              var el = document.getElementById('t');
              var cs = getComputedStyle(el, ':after');
              var mode = cs.content.replace(/["']/g, '');
              var byName = cs.getPropertyValue('display');
              var ok = mode === '' && byName === '' && cs.width === '';
              var a = document.createElement('a'); a.setAttribute('href', ok ? '/ok' : '/bad');
              el.appendChild(a);
            } catch (err) {
              var b = document.createElement('a'); b.setAttribute('href', '/err'); document.getElementById('t').appendChild(b);
            }
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.DoesNotContain("href=\"/err\"", rendered);
        Assert.DoesNotContain("href=\"/bad\"", rendered);
        Assert.Contains("href=\"/ok\"", rendered);
    }

    // A style declaration must answer the `in` operator for every CSS property, set or not, and for the
    // vendor-prefixed spellings a real browser supports. Animation libraries pick the working transform
    // spelling with a prefix probe (`"transform" in style || "WebkitTransform" in style || ...`) and then use
    // the result as a property name with no fallback — GSAP's _checkPropPrefix returns null when none match,
    // and every later transform read throws "Cannot read properties of null (reading 'replace')". A style
    // object that reads a property back but denies it under `in` contradicts itself, so the probe finds
    // nothing even for a property just assigned.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_StyleDeclaration_InOperatorFindsProperties(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            try {
              var st = document.createElement('div').style;
              // GSAP's prefix probe, verbatim in shape: unprefixed first, then vendor-prefixed spellings.
              var kt = "O,Moz,ms,Ms,Webkit".split(",");
              var check = function (e) {
                var _ = 5;
                if (e in st) return e;
                for (e = e.charAt(0).toUpperCase() + e.substr(1); _-- && !(kt[_] + e in st);) ;
                return _ < 0 ? null : (_ === 3 ? "ms" : _ >= 0 ? kt[_] : "") + e;
              };
              st.color = "red";                 // a property this shim itself set must be visible to `in`
              var found = check("transform");   // must resolve to a non-null spelling, not null
              var ok = found !== null && found !== undefined && ("color" in st);
              var a = document.createElement('a'); a.setAttribute('href', ok ? '/ok' : '/bad');
              document.getElementById('t').appendChild(a);
            } catch (err) {
              var b = document.createElement('a'); b.setAttribute('href', '/err'); document.getElementById('t').appendChild(b);
            }
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.DoesNotContain("href=\"/err\"", rendered);
        Assert.DoesNotContain("href=\"/bad\"", rendered);
        Assert.Contains("href=\"/ok\"", rendered);
    }

    // element.style and the style attribute are one property, read from both directions. A page reads
    // `el.style.display` off markup it did not write to decide whether something is already open, and writes
    // `el.style.height` expecting the attribute — and therefore the serialized page — to carry it. Two
    // disconnected stores answer both without complaining: the read is "" whatever the markup says, and the
    // write is invisible to everything that looks at the attribute.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_StyleDeclarationAndTheStyleAttributeAreOneProperty(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body>
            <div id="from-markup" style="display:none;background-color:red">x</div>
            <script>
            var marked = document.getElementById('from-markup');
            var fromMarkup = marked.style.display;
            // The IDL name and the CSS name are the same property.
            var camel = marked.style.backgroundColor;
            marked.style.height = '3px';
            var written = marked.getAttribute('style').indexOf('height: 3px') >= 0;
            marked.setAttribute('style', 'left:2px');
            var afterAttribute = marked.style.left;
            // A value carrying its own semicolon is one declaration, not two.
            var url = document.createElement('div');
            url.setAttribute('style', 'background:url(data:image/svg+xml;base64,AAA);color:blue');
            var probe = document.createElement('a');
            probe.setAttribute('href', '/probe'
              + '?fromMarkup=' + fromMarkup
              + '&camel=' + camel
              + '&written=' + written
              + '&afterAttribute=' + afterAttribute
              + '&afterSemicolon=' + url.style.color);
            document.body.appendChild(probe);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(
            Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), TestContext.Current.CancellationToken);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("fromMarkup=none", rendered);
        Assert.Contains("camel=red", rendered);
        Assert.Contains("written=true", rendered);
        Assert.Contains("afterAttribute=2px", rendered);
        Assert.Contains("afterSemicolon=blue", rendered);
        // The write reached the serialized page, not just the declaration.
        Assert.Contains("left:2px", rendered);
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

    private async Task<string> RenderViewport(JsEngine engine, string html, JsRenderOptions? options)
    {
        var renderer = CreateJsRenderer(engine, options);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        return Encoding.UTF8.GetString(result);
    }

    // Lazy prefetch/lazy-load libraries feature-detect IntersectionObserver support with the classic
    // `'isIntersecting' in IntersectionObserverEntry.prototype` probe (instant-page does exactly this in an
    // IIFE at parse time); the missing global threw ReferenceError before the page hydrated. The global must
    // exist and its prototype must carry isIntersecting for the detection to evaluate instead of aborting.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_IntersectionObserverEntry_GlobalAndPrototypeProbe(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            try {
              var supported = ('IntersectionObserver' in window)
                && ('IntersectionObserverEntry' in window)
                && ('isIntersecting' in window.IntersectionObserverEntry.prototype);
              var a = document.createElement('a'); a.setAttribute('href', '/probe?supported=' + supported);
              document.getElementById('t').appendChild(a);
            } catch (err) {
              var b = document.createElement('a'); b.setAttribute('href', '/err'); document.getElementById('t').appendChild(b);
            }
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.DoesNotContain("href=\"/err\"", rendered);
        Assert.Contains("href=\"/probe?supported=true\"", rendered);
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

    // DOMParser: bundles that parse a fetched or hand-built markup string and query it construct
    // `new DOMParser().parseFromString(html, 'text/html')`; the missing global threw ReferenceError before the
    // bundle assigned the value it derived (a self-hosted Git forge reads its asset-version off a parsed document, so the version
    // global went unset). The parsed result must be a queryable Document — querySelector/getElementById reach the
    // parsed tree — and the constructor must not throw on malformed input.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_DOMParser_ParsesHtmlIntoQueryableDocument(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            try {
              var p = new DOMParser();
              var doc = p.parseFromString('<html><head><meta name="ver" content="1.23.4"></head><body><span id="s">hi</span></body></html>', 'text/html');
              var meta = doc.querySelector('meta[name="ver"]').getAttribute('content');
              var byId = doc.getElementById('s').textContent;
              p.parseFromString('<<<', 'text/html');   // malformed must not throw
              var ok = (doc instanceof Document) && meta === '1.23.4' && byId === 'hi';
              var a = document.createElement('a'); a.setAttribute('href', ok ? '/ok' : '/bad');
              document.getElementById('t').appendChild(a);
            } catch (err) {
              var b = document.createElement('a'); b.setAttribute('href', '/err'); document.getElementById('t').appendChild(b);
            }
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.DoesNotContain("href=\"/err\"", rendered);
        Assert.DoesNotContain("href=\"/bad\"", rendered);
        Assert.Contains("href=\"/ok\"", rendered);
    }

    // XMLSerializer: the inverse of DOMParser, constructed bare during init by the same round-tripping bundles.
    // A missing global is a ReferenceError that aborts the whole render, not one derived value — so the guard is
    // that the anchor lands at all. serializeToString must round-trip a live element back to markup (the same
    // output that backs outerHTML) and must not throw on a non-node input, returning "" instead.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_XMLSerializer_SerializesNodeToMarkup(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            try {
              var s = new XMLSerializer();
              var el = document.createElement('span');
              el.setAttribute('data-k', 'v');
              el.textContent = 'hi';
              var out = s.serializeToString(el);
              var empty = s.serializeToString(null);   // non-node must not throw
              var ok = out.indexOf('data-k="v"') >= 0 && out.indexOf('hi') >= 0 && empty === '';
              var a = document.createElement('a'); a.setAttribute('href', ok ? '/ok' : '/bad');
              document.getElementById('t').appendChild(a);
            } catch (err) {
              var b = document.createElement('a'); b.setAttribute('href', '/err'); document.getElementById('t').appendChild(b);
            }
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.DoesNotContain("href=\"/err\"", rendered);
        Assert.DoesNotContain("href=\"/bad\"", rendered);
        Assert.Contains("href=\"/ok\"", rendered);
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

    // webpack guards a lazy chunk with a 120s setTimeout that rejects the import as a "timeout" unless the
    // script's load event clears it first. A render collapses time, so a delay that long is a give-up guard
    // that must never fire (the resource drain loads the chunk in-turn); a short timer still runs, and an
    // explicit clearTimeout cancels one. Regressed spurious "Loading chunk failed" when setTimeout ignored
    // its delay and clearTimeout was a no-op.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_LongTimersNeverFire_ShortTimersRun_ClearTimeoutCancels(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            var d = document.getElementById('t');
            setTimeout(function () { var a = document.createElement('a'); a.setAttribute('href', '/give-up'); d.appendChild(a); }, 120000);
            setTimeout(function () { var a = document.createElement('a'); a.setAttribute('href', '/short'); d.appendChild(a); }, 0);
            var h = setTimeout(function () { var a = document.createElement('a'); a.setAttribute('href', '/cancelled'); d.appendChild(a); }, 10);
            clearTimeout(h);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/short\"", rendered);
        Assert.DoesNotContain("href=\"/give-up\"", rendered);
        Assert.DoesNotContain("href=\"/cancelled\"", rendered);
    }

    // What a script asks before it decides whether anyone is looking: the tab is visible and focused, the
    // body is what has focus, and a point inside the viewport has an element under it. A session recorder
    // hit-tests its own cursor trail and a bot-management bundle calls document.hasFocus() unguarded, so a
    // missing answer is a throw rather than the backgrounded branch each was written to take. document.fonts
    // is the same shape: a chat widget enumerates families through `fonts.ready.then(s => Array.from(s))`.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_TheDocumentReportsAForegroundPageAndAnswersHitTests(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            var hit = document.elementFromPoint(10, 10);
            var l = document.createElement('a');
            l.setAttribute('href', '/f?focus=' + document.hasFocus() +
                                   '&active=' + document.activeElement.tagName +
                                   '&visible=' + document.visibilityState +
                                   '&hidden=' + document.hidden +
                                   '&hit=' + (hit ? hit.tagName : 'none') +
                                   '&outside=' + document.elementFromPoint(-1, 999999) +
                                   '&stack=' + document.elementsFromPoint(10, 10).length);
            document.body.appendChild(l);
            document.fonts.ready.then(function (set) {
              var f = document.createElement('a');
              f.setAttribute('href', '/fonts?count=' + Array.from(set).length + '&status=' + document.fonts.status);
              document.body.appendChild(f);
            });
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("focus=true", rendered);
        Assert.Contains("active=BODY", rendered);
        Assert.Contains("visible=visible", rendered);
        Assert.Contains("hidden=false", rendered);
        Assert.Contains("hit=BODY", rendered);
        Assert.Contains("outside=null", rendered);
        Assert.Contains("stack=2", rendered);
        Assert.Contains("count=0", rendered);
        Assert.Contains("status=loaded", rendered);
    }

    // The event families a page constructs to drive its own UI, and the pre-constructor spelling a shim
    // built on document.createEvent still uses. Each name is read at module scope, where an absent
    // constructor is `X is not defined` for the whole chunk rather than a feature that quietly does nothing.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_EventConstructorsAndTheLegacyInitializers_AreBothAvailable(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            var click = new MouseEvent('click', { bubbles: true, clientX: 12 });
            var key = new KeyboardEvent('keydown', { key: 'Escape' });
            var legacy = document.createEvent('CustomEvent');
            legacy.initCustomEvent('ready', true, false, { id: 7 });
            var seen = '';
            document.getElementById('t').addEventListener('ready', function (e) { seen = e.type + ':' + e.detail.id; });
            document.getElementById('t').dispatchEvent(legacy);
            var a = document.createElement('a');
            a.setAttribute('href', '/probe?click=' + click.type + click.clientX + '&key=' + key.key
              + '&legacy=' + seen + '&isEvent=' + (legacy instanceof Event));
            document.body.appendChild(a);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("click=click12", rendered);
        Assert.Contains("key=Escape", rendered);
        Assert.Contains("legacy=ready:7", rendered);
        Assert.Contains("isEvent=true", rendered);
    }

    // `document.contains(node)` — the guard a deferred-script loader runs before activating anything it
    // parked. It lived on Element, so the document could not answer it and every parked script was lost.
    // The character-data and shadow types are named the same way: a polyfill reads `.prototype` off each by
    // name before it does anything, and one absent global costs the whole polyfill.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_TheDocumentAnswersContains_AndTheNodeTypeGlobalsExist(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            var host = document.getElementById('t');
            var detached = document.createElement('span');
            var named = ['Text', 'Comment', 'CDATASection', 'ProcessingInstruction', 'ShadowRoot',
                         'CustomElementRegistry'].filter(function (n) { return !window[n] || !window[n].prototype; });
            var a = document.createElement('a');
            a.setAttribute('href', '/probe?in=' + document.contains(host) + '&out=' + document.contains(detached)
              + '&missing=' + (named.length ? named.join(',') : 'none')
              + '&shadow=' + (host.attachShadow({ mode: 'open' }) instanceof ShadowRoot));
            document.body.appendChild(a);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("in=true", rendered);
        Assert.Contains("out=false", rendered);
        Assert.Contains("missing=none", rendered);
        Assert.Contains("shadow=true", rendered);
    }
}
