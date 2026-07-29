using SimpleCrawler.Js.Models;
using SimpleCrawler.Tests.Fixtures;
using SimpleCrawler.Tests.Helpers;
using SimpleCrawler.Tests.Models;
using System.Net;
using System.Text;

namespace SimpleCrawler.Tests;

/// <summary>
/// Resource/script loading, currentScript, XHR/fetch, and the WHATWG Streams shim.
/// </summary>
[Collection("Crawler")]
public class JsDomRendererNetworkingTests : JsDomRendererTestBase, IClassFixture<JsRendererFixture>
{
    public JsDomRendererNetworkingTests(JsRendererFixture fixture) : base(fixture)
    {
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

    // Same reason as the XHR stub, one step further out: a module shim resolving an import map calls fetch bare
    // during init, so with the shim off its absence was a ReferenceError that took the rest of that script —
    // and every global it would have registered — with it. The stub rejects the way a browser rejects a
    // refused request, which is a path the caller already handles, and still issues no request.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_Fetch_IsInertStubWithoutFetchShim(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head><body>
            <script>
            fetch('https://example.test/config.json').then(function () {
              var r = document.createElement('a'); r.setAttribute('href', '/resolved'); document.body.appendChild(r);
            }, function (err) {
              var c = document.createElement('a');
              c.setAttribute('href', err instanceof TypeError ? '/rejected' : '/err');
              document.body.appendChild(c);
            });
            var l = document.createElement('a'); l.setAttribute('href', '/after'); document.body.appendChild(l);
            </script>
            </body></html>
            """;

        var host = new RequestCountingHandler();
        using var client = new HttpClient(host);
        var renderer = CreateJsRenderer(engine, new JsRenderOptions { EnableFetch = false });
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", client, CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        // The trailing anchor proves the bare call didn't abort the script; the rejection proves the page's own
        // error path ran instead.
        Assert.Contains("href=\"/after\"", rendered);
        Assert.Contains("href=\"/rejected\"", rendered);
        Assert.DoesNotContain("href=\"/resolved\"", rendered);
        Assert.Equal(0, host.Requests);
    }

    // A script the host cannot fetch fires the node's error event and costs nothing else. Neither case reaches
    // HttpClient usefully: a blob: URL a bundle built (a module shim rewriting imports appends one) is answered
    // with NotSupportedException, a faulting request with HttpRequestException — raw host exceptions that are
    // not the per-script failure the render isolates, so each aborted the whole page. The module half of the
    // loader always handled both; the script half did not.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_AnUnfetchableScript_FiresErrorAndKeepsThePage(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head><body>
            <script>
            function mark(href) { var a = document.createElement('a'); a.setAttribute('href', href); document.body.appendChild(a); }
            var blob = document.createElement('script');
            blob.src = 'blob:d6f1b0c2-1111-2222-3333-444455556666';
            blob.onerror = function () { mark('/blob-errored'); };
            document.head.appendChild(blob);
            var boom = document.createElement('script');
            boom.src = '/boom.js';
            boom.onerror = function () { mark('/fetch-errored'); };
            document.head.appendChild(boom);
            </script>
            <script>mark('/after');</script>
            </body></html>
            """;

        using var client = new HttpClient(new FaultingHandler());
        // A blob: URL carries no host, so the cross-origin rule would drop it before anything fetched it; the
        // render that exists to observe what a page installs is the one that reaches this.
        var renderer = CreateJsRenderer(engine, new JsRenderOptions { ExecuteCrossOriginScripts = true });
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", client, CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/blob-errored\"", rendered);
        Assert.Contains("href=\"/fetch-errored\"", rendered);
        Assert.Contains("href=\"/after\"", rendered);
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

    /// <summary>Faults every request, the way a live host that stops answering does.</summary>
    private sealed class FaultingHandler : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("connection refused");

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("connection refused");
    }

    /// <summary>404s everything and counts what it was asked for, so "made no request" is asserted rather than assumed.</summary>
    private sealed class RequestCountingHandler : HttpMessageHandler
    {
        private int _requests;

        public int Requests => _requests;

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Send(request, cancellationToken));
    }

    private sealed class StreamBodyHandler : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            => new(HttpStatusCode.OK) { Content = new StringContent("<a href=\"/streamed-body\"></a>") };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Send(request, cancellationToken));
    }
}
