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

    // The document's own getElementsBy* include the root element; an element's search strictly below itself.
    // jQuery resolves a tag-only $("html") through getElementsByTagName, so an empty list there is undefined
    // where the caller expects an element — a CMS bundle reading `$("html").attr("lang").indexOf("-")` at
    // init threw on it and lost every global that script would have registered.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_DocumentGetElementsBy_IncludeTheRootElement(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html lang="sv-SE" class="no-js" name="root"><head></head><body>
            <div id="t" class="no-js"></div>
            <script>
            var byTag = document.getElementsByTagName('html');
            var byClass = document.getElementsByClassName('no-js');
            var byName = document.getElementsByName('root');
            var all = document.getElementsByTagName('*');
            var lang = byTag.length ? byTag[0].getAttribute('lang') : '';
            var l = document.createElement('a');
            l.setAttribute('href', '/r?tag=' + byTag.length +
                                   '&class=' + byClass.length +
                                   '&name=' + byName.length +
                                   '&first=' + (all.length ? all[0].tagName : '') +
                                   '&lang=' + lang.indexOf('-') +
                                   // An element's own search still excludes itself.
                                   '&below=' + document.documentElement.getElementsByTagName('html').length);
            document.body.appendChild(l);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("tag=1", rendered);
        Assert.Contains("class=2", rendered);
        Assert.Contains("name=1", rendered);
        Assert.Contains("first=HTML", rendered);
        Assert.Contains("lang=2", rendered);
        Assert.Contains("below=0", rendered);
    }

    // Sanitizers and text-measuring code build a TreeWalker or a NodeIterator at init and step it; naming
    // NodeFilter for the whatToShow mask is a bare global read, so both the constants and the traversal have
    // to exist or the whole script is lost before it registers anything.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_TreeWalkerAndNodeIterator_WalkFilteredNodesInDocumentOrder(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head><body>
            <div id="root"><p id="one">alpha</p><!-- c --><section id="two"><span id="three">beta</span></section></div>
            <script>
            try {
              var root = document.getElementById('root');
              var walker = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT, null);
              var ids = [];
              while (walker.nextNode()) ids.push(walker.currentNode.id);
              // Back from the last node to the first, which never yields the root itself.
              var back = [];
              while (walker.previousNode()) back.push(walker.currentNode.id);
              var spans = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT, {
                acceptNode: function (n) { return n.localName === 'span' ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_SKIP; }
              });
              var span = spans.nextNode();
              // The iterator sees the same tree, filtered to the text nodes the mask asks for.
              var it = document.createNodeIterator(root, NodeFilter.SHOW_TEXT, null);
              var text = [];
              for (var n = it.nextNode(); n; n = it.nextNode()) text.push(n.data);
              var ok = ids.join(',') === 'one,two,three' &&
                       back.join(',') === 'two,one' &&
                       span.id === 'three' &&
                       spans.nextNode() === null &&
                       text.join(',') === 'alpha,beta';
              var l = document.createElement('a');
              l.setAttribute('href', ok ? '/ok' : '/bad-' + ids.join('.') + '-' + back.join('.') + '-' + text.join('.'));
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

    // classList is a DOMTokenList instance, not a fresh literal per read: bundles test it by identity and
    // polyfills patch the prototype to observe class writes, so the constructor has to be a global and the
    // same element has to hand back the same list. The token operations themselves are unchanged.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_ClassList_IsADomTokenListInstance(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head><body>
            <div id="t" class="a b"></div>
            <script>
            try {
              DOMTokenList.prototype.hasAll = function (names) { return names.every(this.contains, this); };
              var el = document.getElementById('t');
              var list = el.classList;
              el.classList.add('c');
              el.classList.remove('a');
              var ok = typeof DOMTokenList === 'function' &&
                       list instanceof DOMTokenList &&
                       el.classList === list &&
                       list.hasAll(['b', 'c']) &&
                       !list.contains('a') &&
                       list.length === 2 &&
                       list.item(0) === 'b' &&
                       String(list) === 'b c' &&
                       el.getAttribute('class') === 'b c';
              var l = document.createElement('a');
              l.setAttribute('href', ok ? '/ok' : '/bad');
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
        Assert.DoesNotContain("href=\"/bad\"", rendered);
        Assert.Contains("href=\"/ok\"", rendered);
    }

    // document.write is how a legacy tag loader injects itself. Everything is parsed before any script runs
    // here, so the written markup lands at the end of the body — never the browser's post-load behaviour,
    // which implicitly opens the document and would take the whole rendered page with it.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_DocumentWrite_AppendsMarkupWithoutClearingThePage(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head><body>
            <a href="/shell">shell</a>
            <script>
            document.open();
            document.write('<a href="/written">w</a>');
            document.writeln('<a href="/written-line">l</a>');
            document.close();
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/shell\"", rendered);
        Assert.Contains("href=\"/written\"", rendered);
        Assert.Contains("href=\"/written-line\"", rendered);
    }

    // Hydration splits a server-rendered text run where the client tree expects a boundary; without
    // splitText the reconciler throws mid-commit and loses the subtree it was mounting.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_TextSplitText_KeepsHeadAndInsertsTailAsSibling(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head><body>
            <div id="t">alphabeta</div>
            <script>
            try {
              var host = document.getElementById('t');
              var head = host.firstChild;
              var tail = head.splitText(5);
              var ok = head.data === 'alpha' &&
                       tail.data === 'beta' &&
                       head.nextSibling === tail &&
                       tail.parentNode === host &&
                       host.textContent === 'alphabeta';
              var l = document.createElement('a');
              l.setAttribute('href', ok ? '/ok' : '/bad');
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
        Assert.DoesNotContain("href=\"/bad\"", rendered);
        Assert.Contains("href=\"/ok\"", rendered);
    }

    // document.URL/documentURI are the page address read by code that never touches location, and baseURI is
    // what a node resolves its own asset URLs against — the <base href>, resolved against the page URL.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_DocumentUrlAndBaseUri_ReportThePageAddress(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head><base href="/assets/"></head><body>
            <div id="t"></div>
            <script>
            var host = document.getElementById('t');
            var l = document.createElement('a');
            l.setAttribute('href', '/u?url=' + (document.URL === location.href) +
                                   '&uri=' + (document.documentURI === location.href) +
                                   '&base=' + document.baseURI +
                                   '&node=' + (host.baseURI === document.baseURI));
            document.body.appendChild(l);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/deep/page", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("url=true", rendered);
        Assert.Contains("uri=true", rendered);
        Assert.Contains("base=http://localhost:5000/assets/", rendered);
        Assert.Contains("node=true", rendered);
    }
}
