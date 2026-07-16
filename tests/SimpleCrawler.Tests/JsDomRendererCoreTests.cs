using SimpleCrawler.Tests.Fixtures;
using SimpleCrawler.Tests.Helpers;
using SimpleCrawler.Tests.Models;
using System.Text;

namespace SimpleCrawler.Tests;

/// <summary>
/// Core dom.js parsing, mutation, selector matching, and node traversal.
/// </summary>
[Collection("Crawler")]
public class JsDomRendererCoreTests : JsDomRendererTestBase, IClassFixture<JsRendererFixture>
{
    public JsDomRendererCoreTests(JsRendererFixture fixture) : base(fixture)
    {
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
}
