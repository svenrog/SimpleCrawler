using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SimpleCrawler.Js.Models;
using SimpleCrawler.Js.Rendering;
using SimpleCrawler.Tests.Fixtures;
using SimpleCrawler.Tests.Helpers;
using SimpleCrawler.Tests.Models;
using System.Net;
using System.Text;

namespace SimpleCrawler.Tests;

/// <summary>
/// Exercises the pure-JS DOM path (dom.js) without a host: dom.js parses the shell, the inline
/// script mutates the JS DOM, and the tree is serialized back to HTML — no managed DOM wrappers involved.
/// Theory'd over both engines since the JS DOM is the single code path for Jint + V8.
/// </summary>
[Collection("Crawler")]
public class JsDomRendererTests : IClassFixture<JsRendererFixture>
{
    private readonly JsRendererFixture _fixture;

    public JsDomRendererTests(JsRendererFixture fixture)
    {
        _fixture = fixture;
    }

    private JsRenderer CreateJsRenderer(JsEngine engine, JsRenderOptions? options = null, ILogger? logger = null)
    {
        var factory = _fixture.GetFactory(engine);
        return new JsRenderer(factory, options ?? new JsRenderOptions(), logger ?? NullLogger.Instance);
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

    // Emotion's SSR-cache hydration runs document.querySelectorAll('style[data-emotion^="css "]') — the
    // attribute value carries a space. A whitespace-naive rightmost-compound split treated that space as a
    // descendant combinator, produced the garbage tail `"]`, and matchesCompound (matching zero tokens) then
    // returned true for every element, so the selector matched the whole document and Emotion's
    // getAttribute('data-emotion').split(' ') NRE'd on the first attribute-less node, aborting the bundle.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_AttributeSelectorWithSpacedValue_DoesNotMatchEveryElement(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head>
            <style data-emotion="css 1a2b 3c4d">.x{}</style>
            <style data-emotion="css">.y{}</style>
            </head><body><div>a</div><span>b</span>
            <script>
            try {
              var m = document.querySelectorAll('style[data-emotion^="css "]');
              var keys = [];
              Array.prototype.forEach.call(m, function (e) {
                keys = keys.concat(e.getAttribute('data-emotion').split(' ').slice(1));
              });
              var a = document.createElement('a');
              a.setAttribute('href', '/ok-' + m.length + '-' + keys.join('.'));
              document.body.appendChild(a);
            } catch (err) {
              var b = document.createElement('a'); b.setAttribute('href', '/err'); document.body.appendChild(b);
            }
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.DoesNotContain("href=\"/err\"", rendered);
        Assert.Contains("href=\"/ok-1-1a2b.3c4d\"", rendered);
    }

    // A React componentDidMount that holds a ref to an input and calls inputRef.current.focus() crashed the
    // bundle ("focus is not a function"): focus()/blur() existed only on the iframe stub, not on HTMLElement.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_HtmlElementFocusAndBlur_AreCallable(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head><body><input id="q" />
            <script>
            try {
              var input = document.getElementById('q');
              input.focus();
              input.blur();
              var a = document.createElement('a'); a.setAttribute('href', '/ok'); document.body.appendChild(a);
            } catch (err) {
              var b = document.createElement('a'); b.setAttribute('href', '/err'); document.body.appendChild(b);
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

    // jQuery/Sizzle feature-detects native selection with /\{\s*\[native code/ against our methods; a plain-JS
    // querySelectorAll/getElementsByClassName fails that test, so for a bare `.class` selector Sizzle falls back
    // to enumerating every element via getElementsByTagName("*") and filtering by className. That wildcard used
    // to match nothing (no element's localName is "*"), so $(".x") came back empty even though the element was
    // in the DOM — which left jQuery UI autocomplete's `.data("ui-autocomplete")` undefined and crashed init.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_GetElementsByTagNameWildcard_EnumeratesAllElements(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><head></head><body>
            <form><div class="wrap"><input class="form-control typeahead ui-autocomplete-input" type="text"></div></form>
            <div id="t"></div>
            <script>
            var all = document.getElementsByTagName('*');
            var byClassFallback = 0, found = 'none';
            for (var i = 0; i < all.length; i++) {
              var cls = all[i].className || '';
              if (/(^|\s)typeahead(\s|$)/.test(cls)) { byClassFallback++; found = all[i].tagName; }
            }
            var out = document.createElement('a');
            out.setAttribute('href', '/probe?all=' + (all.length > 0) + '&hits=' + byClassFallback + '&tag=' + found);
            document.getElementById('t').appendChild(out);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://example.test/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("all=true", rendered);
        Assert.Contains("hits=1", rendered);
        Assert.Contains("tag=INPUT", rendered);
    }

    // jQuery/Sizzle only take their fast native-selection paths when a method stringifies to "[native code]"
    // (support.qsa/getElementsByClassName/matchesSelector = rnative.test(fn)); plain-JS host methods failed that
    // and forced the slow manual matcher. Host DOM methods now report a native-looking toString so the probe
    // passes — while ordinary bundle functions keep their real source (only marked host methods are affected).
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_HostMethodsReportNativeCode_UserFunctionsDoNot(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><input class="x" /><div id="t"></div>
            <script>
            var rnative = /^[^{]+\{\s*\[native code/;
            var qsa = rnative.test(document.querySelectorAll);
            var gebcn = rnative.test(document.getElementsByClassName);
            var matches = rnative.test(document.getElementById('t').matches);
            function userFn() { return 42; }
            var userNative = rnative.test(userFn);
            var out = document.createElement('a');
            out.setAttribute('href', '/probe?qsa=' + qsa + '&gebcn=' + gebcn + '&matches=' + matches + '&user=' + userNative);
            document.getElementById('t').appendChild(out);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://example.test/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("/probe?qsa=true", rendered);
        Assert.Contains("gebcn=true", rendered);
        Assert.Contains("matches=true", rendered);
        Assert.Contains("user=false", rendered);
    }

    // jQuery gates .offset() and visibility on `elem.getClientRects().length` before touching the box (jQuery UI
    // autocomplete positions its menu through that path). A connected element reports one rect, a detached one
    // none; the method missing entirely threw "getClientRects is not a function" once autocomplete init ran.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_GetClientRects_ReflectsConnectedness(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            var connected = document.getElementById('t').getClientRects().length;
            var detached = document.createElement('div').getClientRects().length;
            var out = document.createElement('a');
            out.setAttribute('href', '/probe?connected=' + connected + '&detached=' + detached);
            document.getElementById('t').appendChild(out);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://example.test/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("connected=1", rendered);
        Assert.Contains("detached=0", rendered);
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

    // iframe.contentWindow.postMessage — consent/widget bundles post to an embedded frame and crash on
    // undefined without it; the frame element must also be a real HTMLIFrameElement so `instanceof` identifies
    // it. Both the parsed shell iframe and a createElement'd one carry contentWindow.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_IframeContentWindowPostMessage(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div><iframe id="f" src="/frame"></iframe>
            <script>
            var d = document.getElementById('t');
            var parsed = document.getElementById('f');
            parsed.contentWindow.postMessage({ cmd: 'ping' }, '*');
            var created = document.createElement('iframe');
            created.contentWindow.postMessage('x');
            var ok = (parsed instanceof HTMLIFrameElement) && (created instanceof HTMLIFrameElement);
            var a = document.createElement('a'); a.setAttribute('href', ok ? '/iframe' : '/fail'); a.textContent = 'f';
            d.appendChild(a);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/iframe\"", rendered);
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

    // Custom-element connectedCallbacks flip boolean attributes via el.toggleAttribute(name[, force]);
    // returns the resulting presence, honours the optional force arg, and mutates the live attribute.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_ToggleAttribute_FlipsAndHonoursForce(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            var d = document.getElementById('t');
            var on = d.toggleAttribute('hidden');            // absent -> add, returns true
            var off = d.toggleAttribute('hidden');           // present -> remove, returns false
            var forced = d.toggleAttribute('data-x', true);  // force add
            d.toggleAttribute('data-x', true);               // idempotent
            var a = document.createElement('a');
            a.setAttribute('href', '/' + on + '-' + off + '-' + forced + '-has' + d.hasAttribute('data-x'));
            d.appendChild(a);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/true-false-true-hastrue\"", rendered);
    }

    // Map/3D libraries (Mapbox GL's Painter, Three.js, deck.gl) initialize WebGL synchronously while
    // constructing — canvas.getContext("webgl2"), then querying limits, compiling a shader, linking a program
    // and checking framebuffer completeness — and throw "Failed to initialize WebGL." on a null context, an
    // uncaught throw that trips the SPA error boundary and drops every anchor. EnableWebGl hands back a stub
    // that reports success through that whole sequence so the surrounding page still renders. Off by default:
    // getContext("webgl") stays null, so this is opt-in exactly like fetch/streams.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_WebGl_OptIn_SurvivesContextInitialization(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            function setupPainter() {
              var canvas = document.createElement('canvas');
              var gl = canvas.getContext('webgl2', { antialias: true }) || canvas.getContext('webgl');
              if (!gl) throw new Error('Failed to initialize WebGL.');
              var maxTex = gl.getParameter(gl.MAX_TEXTURE_SIZE);
              var dbg = gl.getExtension('WEBGL_debug_renderer_info');
              var renderer = dbg ? gl.getParameter(dbg.UNMASKED_RENDERER_WEBGL) : '';
              var vs = gl.createShader(gl.VERTEX_SHADER);
              gl.shaderSource(vs, 'void main(){}');
              gl.compileShader(vs);
              if (!gl.getShaderParameter(vs, gl.COMPILE_STATUS)) throw new Error('shader');
              var prog = gl.createProgram();
              gl.attachShader(prog, vs);
              gl.linkProgram(prog);
              if (!gl.getProgramParameter(prog, gl.LINK_STATUS)) throw new Error('link');
              gl.bindBuffer(gl.ARRAY_BUFFER, gl.createBuffer());
              if (gl.checkFramebufferStatus(gl.FRAMEBUFFER) !== gl.FRAMEBUFFER_COMPLETE) throw new Error('fbo');
              return { maxTex: maxTex, renderer: renderer };
            }
            var d = document.getElementById('t');
            try {
              var p = setupPainter();
              var a = document.createElement('a');
              a.setAttribute('href', '/map-ok?tex=' + p.maxTex + '&r=' + p.renderer);
              d.appendChild(a);
            } catch (err) {
              var b = document.createElement('a'); b.setAttribute('href', '/map-err'); d.appendChild(b);
            }
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine, new JsRenderOptions { EnableWebGl = true });
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.DoesNotContain("href=\"/map-err\"", rendered);
        Assert.Contains("href=\"/map-ok?tex=4096", rendered);
        Assert.Contains("r=SimpleCrawler WebGL\"", rendered);
    }

    // The flip side: with EnableWebGl off (the default), getContext("webgl"/"webgl2") returns null so the crawl
    // pays nothing for pages that never touch WebGL. A 2D context is always available regardless of the flag.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_WebGl_DefaultOff_ReturnsNullContext(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            var canvas = document.createElement('canvas');
            var gl = canvas.getContext('webgl2') || canvas.getContext('webgl');
            var has2d = !!canvas.getContext('2d');
            var a = document.createElement('a');
            a.setAttribute('href', '/webgl-' + (gl === null) + '-2d-' + has2d);
            document.getElementById('t').appendChild(a);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/webgl-true-2d-true\"", rendered);
    }

    private async Task<string> RenderViewport(JsEngine engine, string html, JsRenderOptions? options)
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

    // The canonical inline-bootstrap self-removal, `(document.currentScript || last script).parentNode
    // .removeChild(self)`. currentScript is a synthetic, detached element, so its parentNode must resolve to a
    // live container and the removal must be a harmless no-op — otherwise it dereferences null and aborts the
    // rest of the script. The trailing anchor proves execution continued past it.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_CurrentScript_SelfRemoval_DoesNotAbortScript(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            var e = document.currentScript || document.scripts[document.scripts.length - 1];
            e.parentNode.removeChild(e);
            var a = document.createElement('a');
            a.setAttribute('href', '/after-self-remove');
            document.getElementById('t').appendChild(a);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/after-self-remove\"", rendered);
    }

    // window.Blob backs bundles that build object URLs or read blob bytes; a missing global threw
    // ReferenceError into the SPA error boundary. Size (bytes across string + typed-array parts), type, and
    // URL.createObjectURL must all resolve.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_Blob_IsAvailable(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            var b = new Blob(['hello ', new Uint8Array([119,111,114,108,100])], { type: 'text/plain' });
            var url = URL.createObjectURL(b);
            var a = document.createElement('a');
            a.setAttribute('href', '/blob-' + b.size + '-' + b.type + '-' + (url.indexOf('blob:') === 0));
            document.getElementById('t').appendChild(a);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/blob-11-text/plain-true\"", rendered);
    }

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_FormControlsExposeConstraintValidation(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head><body><input id="q" />
            <script>
            try {
              var input = document.getElementById('q');
              input.setCustomValidity('oops');
              var ok = input.willValidate === true &&
                       input.checkValidity() === true &&
                       input.reportValidity() === true &&
                       input.validationMessage === '' &&
                       input.validity.valid === true &&
                       input.validity.valueMissing === false;
              var a = document.createElement('a'); a.setAttribute('href', ok ? '/ok' : '/bad'); document.body.appendChild(a);
            } catch (err) {
              var b = document.createElement('a'); b.setAttribute('href', '/err'); document.body.appendChild(b);
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

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_FileListGlobal_IsAvailable(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            try {
              var list = new FileList();
              var spread = 0;
              for (var f of list) spread++;
              var ok = typeof FileList === 'function' &&
                       list instanceof FileList &&
                       list.length === 0 &&
                       list.item(0) === null &&
                       spread === 0;
              var a = document.createElement('a'); a.setAttribute('href', ok ? '/ok' : '/bad'); document.getElementById('t').appendChild(a);
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

    // Canvas animation libraries (lottie, confetti) mount by grabbing a 2D context synchronously and calling
    // draw methods on it — `canvas.getContext('2d')` then fillRect/measureText/... — so a <canvas> must be a
    // real HTMLCanvasElement with getContext returning a no-op context, not the plain HTMLElement it used to
    // parse/createElement as (which had no getContext and threw "getContext is not a function").
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_CanvasGetContext2D_ReturnsUsableNoOpContext(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><canvas id="parsed" width="640" height="480"></canvas><div id="t"></div>
            <script>
            try {
              var canvas = document.createElement('canvas');
              var ctx = canvas.getContext('2d');
              ctx.save(); ctx.beginPath(); ctx.moveTo(0, 0); ctx.lineTo(10, 10); ctx.fillRect(0, 0, 5, 5); ctx.restore();
              var w = ctx.measureText('x').width;
              var g = ctx.createLinearGradient(0, 0, 1, 1); g.addColorStop(0, '#000');
              var parsed = document.getElementById('parsed');
              var ok = (canvas instanceof HTMLCanvasElement)
                && (parsed instanceof HTMLCanvasElement)
                && typeof ctx.fillRect === 'function'
                && w === 0
                && canvas.width === 300
                && parsed.width === 640
                && canvas.getContext('webgl') === null;
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

    // A page served from a nested path with <base href="/"> resolves a relative <script src> against the
    // base, not the page URL. Without that, the host fetches /nested/app.js — which an SPA serves as its HTML
    // catch-all fallback — and the engine aborts on "Unexpected token <" instead of running the bundle.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_RelativeScript_ResolvesAgainstBaseHref(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head><base href="/"></head>
            <body><script src="app.js"></script></body></html>
            """;

        var handler = new BaseHrefHandler();
        using var client = new HttpClient(handler);
        var renderer = CreateJsRenderer(engine);

        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://example.test/page/Start2/", client, CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Equal("https://example.test/app.js", handler.RequestedScriptUrl);
        Assert.Contains("href=\"/from-base\"", rendered);
    }

    // Zone.js (Angular/RxJS) patches XHR event listeners on XMLHttpRequestEventTarget.prototype and, when
    // scheduling the request, reads the original addEventListener off it to attach its own readystatechange
    // listener. XHR must therefore be an XMLHttpRequestEventTarget (registered globally) whose send dispatches
    // to addEventListener listeners, not only the on* handlers — otherwise Zone throws before send runs.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_Xhr_IsEventTarget_AndDispatchesLoadListener(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head>
            <body><div id="r"></div>
            <script>
              var x = new XMLHttpRequest();
              if (x instanceof XMLHttpRequestEventTarget) {
                x.addEventListener('load', function () {
                  var a = document.createElement('a');
                  a.setAttribute('href', '/xhr-' + x.status);
                  document.body.appendChild(a);
                });
                x.open('GET', '/api');
                x.send();
              }
            </script>
            </body></html>
            """;

        var handler = new FixedResponseHandler();
        using var client = new HttpClient(handler);
        var renderer = CreateJsRenderer(engine, new JsRenderOptions { EnableFetch = true });

        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://example.test/", client, CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/xhr-200\"", rendered);
    }

    // A marquee/virtualization component sizes itself by dividing a container measurement by an element's
    // offsetWidth, then spreads `[...Array(count)]`. offsetWidth used to be undefined (and clientWidth is 0
    // for non-root), so count came out NaN and `new Array(NaN)` threw "Invalid array length", tripping the
    // SPA error boundary. A nonzero offsetWidth keeps the ratio finite so the spread — and the render — survive.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_OffsetWidthIsNonzero_KeepsLayoutRatioFinite(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><body>
            <div id="c"><span id="i">x</span></div>
            <script>
              var c=document.getElementById('c'), i=document.getElementById('i');
              var count=Math.ceil(c.clientWidth/(i.offsetWidth+0))+1;
              var arr=[...Array(count)];
              var a=document.createElement('a');
              a.setAttribute('href','/ok-'+Number.isFinite(count)+'-len'+arr.length);
              document.body.appendChild(a);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://example.test/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/ok-true-len1\"", rendered);
    }

    // A <video>/<audio> ref's mount effect calls el.load()/play()/pause() synchronously. Those were missing
    // on the generic element a <video> parsed into, so the effect threw and blanked the page. Media elements
    // now carry inert versions and are instanceof HTMLMediaElement.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_MediaElement_HasInertPlaybackApi(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><body>
            <script>
              var out='/media';
              try {
                var v=document.createElement('video');
                var ok = (v instanceof HTMLVideoElement) && (v instanceof HTMLMediaElement);
                v.onloadeddata=function(){};
                v.load(); v.play(); v.pause();
                out='/media-'+ok;
              } catch (e) { out='/err-'+e.message; }
              var a=document.createElement('a');
              a.setAttribute('href',out);
              document.body.appendChild(a);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://example.test/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/media-true\"", rendered);
    }

    // A modal component refs its <dialog> and calls showModal()/close() in a mount effect to sync visibility
    // (open ? el.showModal() : el.close()). Those were missing on the generic element a <dialog> parsed into,
    // so `el.close is not a function` threw and tripped the SPA error boundary. Dialogs now carry the inert
    // open/show/showModal/close API and are instanceof HTMLDialogElement.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_DialogElement_HasInertModalApi(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><body>
            <script>
              var out='/dialog';
              try {
                var d=document.createElement('dialog');
                var isType = d instanceof HTMLDialogElement;
                d.showModal();
                var opened = d.open;
                d.close('ok');
                out='/dialog-'+isType+'-'+opened+'-'+d.open+'-'+d.returnValue;
              } catch (e) { out='/err-'+e.message; }
              var a=document.createElement('a');
              a.setAttribute('href',out);
              document.body.appendChild(a);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://example.test/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/dialog-true-true-false-ok\"", rendered);
    }

    // The global Response is a spec-compliant constructor the page's own bundle may call directly
    // (new Response(body, init), or new Response() with no args) — not only the internal wrapper fetch
    // builds. A bundle that did `new Response()` and read `.ok` crashed ("Cannot read properties of
    // undefined (reading ok)") because the constructor treated its first arg as the host-fetch result.
    // Default status is 200/ok, an explicit status drives ok, and json()/text() read the body.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_ResponseConstructor_IsSpecCompliant(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head>
            <body><div id="t"></div>
            <script>
            (async function () {
              try {
                var empty = new Response();
                var made = new Response('{"v":42}', { status: 404, statusText: 'Nope', headers: { 'Content-Type': 'application/json' } });
                var body = await made.json();
                var out = '/resp-' + empty.ok + '-' + empty.status + '-' + made.ok + '-' + made.status + '-' + body.v + '-' + made.headers.get('content-type');
                var a = document.createElement('a'); a.setAttribute('href', out); document.getElementById('t').appendChild(a);
              } catch (e) {
                var b = document.createElement('a'); b.setAttribute('href', '/err-' + e.message); document.getElementById('t').appendChild(b);
              }
            })();
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine, new JsRenderOptions { EnableFetch = true });
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://example.test/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/resp-true-200-false-404-42-application/json\"", rendered);
    }

    // A production bundle gates its runtime data cache on `window.indexedDB` being present and functional.
    // With no shim the feature-detect fails and the cache is bypassed, so every render re-requests the same
    // data forever (an endless fetch storm that never settles). The shim must both satisfy the detect and
    // round-trip a value through open → onupgradeneeded/createObjectStore → transaction → put → get.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_IndexedDB_RoundTripsAcrossTransactions(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head><title>t</title></head><body>
            <script>
            if (window.indexedDB && typeof indexedDB.open === 'function') {
                var open = indexedDB.open('crawler-db', 1);
                open.onupgradeneeded = function (e) { e.target.result.createObjectStore('kv'); };
                open.onsuccess = function (e) {
                    var db = e.target.result;
                    var write = db.transaction('kv', 'readwrite');
                    write.objectStore('kv').put('value-42', 'the-key');
                    write.oncomplete = function () {
                        var read = db.transaction('kv', 'readonly');
                        var got = read.objectStore('kv').get('the-key');
                        read.oncomplete = function () {
                            if (got.result === 'value-42') {
                                var a = document.createElement('a');
                                a.setAttribute('href', '/idb/' + got.result);
                                document.body.appendChild(a);
                            }
                        };
                    };
                };
            }
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine, new JsRenderOptions { EnableIndexedDb = true });
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://example.test/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/idb/value-42\"", rendered);
    }

    private sealed class FixedResponseHandler : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            => new(HttpStatusCode.OK) { Content = new StringContent("{}") };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Send(request, cancellationToken));
    }

    /// <summary>
    /// Serves JS only for the base-resolved script URL; any other path returns the SPA's HTML fallback, so a
    /// page-relative resolution fetches HTML and the injected anchor never appears.
    /// </summary>
    private sealed class BaseHrefHandler : HttpMessageHandler
    {
        public string? RequestedScriptUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url == "https://example.test/app.js")
            {
                RequestedScriptUrl = url;
                const string js = "var a=document.createElement('a');a.setAttribute('href','/from-base');document.body.appendChild(a);";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(js) });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<!doctype html><html><body>fallback</body></html>") });
        }
    }

    // EnableStreams installs a WHATWG Streams shim: a standalone ReadableStream can be driven to completion
    // through getReader().read(), with chunks decoded back to text via TextDecoder. Read promises settle on
    // the same drain that runs the rest of the render, so an anchor injected only after the stream ends still
    // makes it into the output on both engines.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_Streams_StandaloneReadableStream_DrainsToText(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><body>
            <script>
              var rs = new ReadableStream({
                start: function (c) { c.enqueue(new TextEncoder().encode('<a href="/streamed"></a>')); c.close(); }
              });
              var reader = rs.getReader();
              var dec = new TextDecoder();
              (function pump(acc) {
                reader.read().then(function (res) {
                  if (res.done) {
                    var m = acc.match(/href="([^"]+)"/);
                    var a = document.createElement('a');
                    a.setAttribute('href', m ? m[1] : '/nomatch');
                    document.body.appendChild(a);
                    return;
                  }
                  pump(acc + dec.decode(res.value));
                });
              })('');
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine, new JsRenderOptions { EnableStreams = true });
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://example.test/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/streamed\"", rendered);
    }

    // With both flags on, Response.body is a readable stream over the buffered body and pipes through a
    // TextDecoderStream — the idiom RSC/Flight consumers use to read a response. The link only exists inside
    // the fetched payload, so its presence proves the body streamed through the transform and decoded.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_Streams_ResponseBody_PipesThroughTextDecoderStream(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><body>
            <script>
              fetch('/data').then(function (res) {
                var reader = res.body.pipeThrough(new TextDecoderStream()).getReader();
                (function pump(acc) {
                  reader.read().then(function (r) {
                    if (r.done) {
                      var m = acc.match(/href="([^"]+)"/);
                      var a = document.createElement('a');
                      a.setAttribute('href', m ? m[1] : '/nomatch');
                      document.body.appendChild(a);
                      return;
                    }
                    pump(acc + r.value);
                  });
                })('');
              });
            </script>
            </body></html>
            """;

        using var client = new HttpClient(new StreamBodyHandler());
        var renderer = CreateJsRenderer(engine, new JsRenderOptions { EnableFetch = true, EnableStreams = true });
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://example.test/", client, CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/streamed-body\"", rendered);
    }

    // The flag is genuinely off by default: no stream globals are installed and Response.body stays null,
    // exactly as a browser without a stream body. Fetch is enabled here only so the Response global exists to
    // probe; streams remain absent.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_Streams_OffByDefault_NoGlobals_AndNullBody(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><body>
            <script>
              var out;
              if (typeof ReadableStream !== 'undefined') out = '/stream-defined';
              else out = new Response('x').body === null ? '/no-stream' : '/has-body';
              var a = document.createElement('a');
              a.setAttribute('href', out);
              document.body.appendChild(a);
            </script>
            </body></html>
            """;

        using var client = new HttpClient(new FixedResponseHandler());
        var renderer = CreateJsRenderer(engine, new JsRenderOptions { EnableFetch = true });
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://example.test/", client, CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/no-stream\"", rendered);
    }

    // Safety net for streaming/hydration bundles: if the script path tears down the server-rendered tree
    // and this single-pass render can't rebuild it (the Next.js RSC failure mode), the renderer must not
    // ship fewer links than the shell arrived with. Here a script wipes the body; with EnableStreams the
    // pre-script baseline (its two SSR anchors) is restored rather than emitting an empty page.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_Streams_RegressionGuard_RestoresBaselineWhenBundleWipesSsr(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><body>
            <a href="/ssr-1">one</a><a href="/ssr-2">two</a>
            <script>document.body.innerHTML = '';</script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine, new JsRenderOptions { EnableStreams = true });
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://example.test/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/ssr-1\"", rendered);
        Assert.Contains("href=\"/ssr-2\"", rendered);
    }

    // The guard must never clobber a healthy render: when the bundle adds links (the normal SPA case), the
    // richer post-script tree is kept because it does not regress below the baseline anchor count.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_Streams_RegressionGuard_KeepsRicherRenderWhenBundleAddsAnchors(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><body>
            <a href="/ssr-1">one</a>
            <script>
              var d = document.body;
              ['/added-1', '/added-2'].forEach(function (h) {
                var a = document.createElement('a'); a.setAttribute('href', h); d.appendChild(a);
              });
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine, new JsRenderOptions { EnableStreams = true });
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://example.test/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/ssr-1\"", rendered);
        Assert.Contains("href=\"/added-1\"", rendered);
        Assert.Contains("href=\"/added-2\"", rendered);
    }

    // Element-level scroll methods. A component that scrolls an element while initializing (a carousel, a
    // sticky nav, a cookie banner) calls these on its way to building the rest of its subtree; the single-pass
    // render never scrolls, so they no-op — but a *missing* method throws, and the throw lands inside that
    // init, costing everything below it. Only the window-level shims existed, which is the half a page
    // rarely calls.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_ElementScrollMethods_NoOpRatherThanThrowingAndCostingTheSubtree(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><body><div id="r"></div>
            <script>
              var host = document.getElementById('r');
              host.scrollTo({ left: 0, behavior: 'smooth' });
              host.scrollBy(0, 10);
              host.scroll(0, 0);
              host.scrollIntoView({ block: 'center' });
              var a = document.createElement('a');
              a.setAttribute('href', '/after-scroll');
              document.body.appendChild(a);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://example.test/", new HttpClient(), CancellationToken.None);

        Assert.Contains("href=\"/after-scroll\"", Encoding.UTF8.GetString(result));
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

    // Focus/tab-order libraries sort DOM nodes with an `a.compareDocumentPosition(b)` comparator, inside a
    // useMemo during render. Without the method the sort throws, the render subtree fails, and every effect
    // below it (e.g. an SDK's init) silently never runs.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_CompareDocumentPosition_OrdersNodesByDocumentPosition(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head><body>
            <div id="a"></div><div id="b"><span id="c"></span></div>
            <script>
            try {
              var a = document.getElementById('a'), b = document.getElementById('b'), c = document.getElementById('c');
              var F = Node.DOCUMENT_POSITION_FOLLOWING, P = Node.DOCUMENT_POSITION_PRECEDING,
                  CB = Node.DOCUMENT_POSITION_CONTAINED_BY;
              var ok = (a.compareDocumentPosition(b) & F) &&      // b follows a
                       (b.compareDocumentPosition(a) & P) &&      // a precedes b
                       (b.compareDocumentPosition(c) & CB) &&     // c is inside b
                       (a.compareDocumentPosition(a) === 0);
              // A tab-order sort: nodes must come out in document order without throwing.
              var sorted = [c, b, a].sort(function (x, y) {
                return (x.compareDocumentPosition(y) & F) ? -1 : 1;
              }).map(function (n) { return n.id; }).join('');
              var l = document.createElement('a');
              l.setAttribute('href', ok && sorted === 'abc' ? '/ok' : '/err');
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

    // AbortController/AbortSignal are general Web APIs (timeouts, cancellation), not fetch-specific, so an SDK
    // that does `new AbortController()` during init must not throw on the default render, where the fetch
    // shim is off.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_AbortController_IsPresentWithoutFetchShim(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head><body>
            <script>
            try {
              var c = new AbortController();
              var ok = typeof AbortController !== 'undefined' && typeof AbortSignal !== 'undefined' &&
                       c.signal && c.signal.aborted === false && typeof c.abort === 'function';
              var l = document.createElement('a'); l.setAttribute('href', ok ? '/ok' : '/err');
              document.body.appendChild(l);
            } catch (err) {
              var e = document.createElement('a'); e.setAttribute('href', '/err'); document.body.appendChild(e);
            }
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine, new JsRenderOptions { EnableFetch = false });
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.DoesNotContain("href=\"/err\"", rendered);
        Assert.Contains("href=\"/ok\"", rendered);
    }

    // The base prelude provides an inert XMLHttpRequest so an SDK that patches XMLHttpRequest.prototype.open
    // unguarded at init doesn't throw when the fetch shim is off. send() must not reach for __http (absent
    // without the shim), so it makes no request and doesn't throw.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_XmlHttpRequest_IsInertStubWithoutFetchShim(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head><body>
            <script>
            try {
              var patched = false, origOpen = XMLHttpRequest.prototype.open;
              XMLHttpRequest.prototype.open = function () { patched = true; return origOpen.apply(this, arguments); };
              var x = new XMLHttpRequest();
              x.open('GET', 'https://example.test/beacon');
              x.setRequestHeader('a', 'b');
              x.send('{}');
              var ok = typeof XMLHttpRequest !== 'undefined' && patched && x.readyState === 1 &&
                       typeof x.addEventListener === 'function';
              var l = document.createElement('a'); l.setAttribute('href', ok ? '/ok' : '/err');
              document.body.appendChild(l);
            } catch (err) {
              var e = document.createElement('a'); e.setAttribute('href', '/err'); document.body.appendChild(e);
            }
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine, new JsRenderOptions { EnableFetch = false });
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.DoesNotContain("href=\"/err\"", rendered);
        Assert.Contains("href=\"/ok\"", rendered);
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

    // Layout libraries construct DOMRect/DOMRectReadOnly bare during init and read the derived edges; absence
    // is a ReferenceError inside the mount. The edges must derive from the supplied box.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_DomRect_ConstructsAndDerivesEdges(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head><body>
            <script>
            try {
              var r = new DOMRect(10, 20, 30, 40);
              var f = DOMRectReadOnly.fromRect({ x: 1, y: 2, width: 3, height: 4 });
              var ok = r.x === 10 && r.y === 20 && r.width === 30 && r.height === 40 &&
                       r.left === 10 && r.top === 20 && r.right === 40 && r.bottom === 60 &&
                       r instanceof DOMRectReadOnly &&
                       f.right === 4 && f.bottom === 6;
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

    // Graphics widgets composite off-screen with `new OffscreenCanvas(w, h).getContext("2d")` then draw; the
    // global must exist and hand back the same no-op 2d context as <canvas> (null for other context types).
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_OffscreenCanvas_ReturnsInert2dContext(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head><body>
            <script>
            try {
              var c = new OffscreenCanvas(64, 32);
              var ctx = c.getContext('2d');
              ctx.clearRect(0, 0, 64, 32);
              var ok = c.width === 64 && c.height === 32 && ctx && typeof ctx.clearRect === 'function' &&
                       typeof ctx.drawImage === 'function' && c.getContext('webgl') === null &&
                       typeof c.convertToBlob().then === 'function';
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

    // Animation-sequencing code awaits `element.animate(...).finished` (and `.ready`) then calls
    // `.commitStyles()`; the inert Animation must expose those as settled promises and methods, or the exact
    // idiom `r.finished.then(...)` throws on undefined and fails the effect.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_Animate_ExposesFinishedAndReadyPromises(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head><body><div id="t"></div>
            <script>
            try {
              var r = document.getElementById('t').animate([{ opacity: 0 }, { opacity: 1 }], { duration: 100 });
              var ok = r && typeof r.finished.then === 'function' && typeof r.ready.then === 'function' &&
                       typeof r.commitStyles === 'function' && typeof r.persist === 'function' &&
                       typeof r.cancel === 'function';
              // The failure-mode idiom must not throw.
              r.finished.then(function () { });
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

    private sealed class StreamBodyHandler : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            => new(HttpStatusCode.OK) { Content = new StringContent("<a href=\"/streamed-body\"></a>") };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Send(request, cancellationToken));
    }
}
