using SimpleCrawler.Js.Models;
using SimpleCrawler.Tests.Fixtures;
using SimpleCrawler.Tests.Helpers;
using SimpleCrawler.Tests.Models;
using System.Text;

namespace SimpleCrawler.Tests;

/// <summary>
/// Element-specific dom.js behaviors: form controls, custom elements, media/canvas/dialog inert APIs, and
/// layout-adjacent element methods.
/// </summary>
[Collection("Crawler")]
public class JsDomRendererElementsTests : JsDomRendererTestBase, IClassFixture<JsRendererFixture>
{
    public JsDomRendererElementsTests(JsRendererFixture fixture) : base(fixture)
    {
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
}
