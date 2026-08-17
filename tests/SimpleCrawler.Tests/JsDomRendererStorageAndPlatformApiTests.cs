using SimpleCrawler.Js.Models;
using SimpleCrawler.Tests.Fixtures;
using SimpleCrawler.Tests.Helpers;
using SimpleCrawler.Tests.Models;
using System.Text;

namespace SimpleCrawler.Tests;

/// <summary>
/// Storage, crypto, timing, and other standalone Web Platform API globals.
/// </summary>
[Collection("Crawler")]
public class JsDomRendererStorageAndPlatformApiTests : JsDomRendererTestBase, IClassFixture<JsRendererFixture>
{
    public JsDomRendererStorageAndPlatformApiTests(JsRendererFixture fixture) : base(fixture)
    {
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

    // Storage is also a global constructor: a bundle that tests storage by identity rather than by presence
    // (`x instanceof Storage`) or patches Storage.prototype to observe writes names it bare, so its absence was
    // a ReferenceError during init — and the anchor below, like every global that script would have registered,
    // was lost with it. The instances were always installed; only the type was missing.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_StorageConstructorIsAGlobal(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            var reads = 0;
            var original = Storage.prototype.getItem;
            Storage.prototype.getItem = function (key) { reads++; return original.call(this, key); };
            localStorage.setItem('a', '/typed');
            var ok = localStorage instanceof Storage && sessionStorage instanceof Storage &&
                     localStorage.getItem('a') === '/typed' && reads === 1;
            var a = document.createElement('a'); a.setAttribute('href', ok ? '/typed' : '/fail'); a.textContent = 's';
            document.getElementById('t').appendChild(a);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/typed\"", rendered);
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

    // The navigation-timing pair an analytics SDK reads at init: the legacy performance.timing (epoch
    // milliseconds, subtracted from each other) and its Level 2 replacement, the entry
    // getEntriesByType("navigation")[0]. Both report a render that fetched nothing and painted nothing, so
    // every phase is the same instant — an absent surface is a TypeError inside the SDK, a zero-length phase
    // is only an uninteresting measurement.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_PerformanceTimingAndNavigationEntry_AreReadable(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            try {
              var t = performance.timing;
              var nav = performance.getEntriesByType('navigation')[0];
              var ok = typeof t.navigationStart === 'number' && t.navigationStart > 0 &&
                       t.domInteractive - t.navigationStart === 0 &&
                       t.loadEventEnd >= t.navigationStart &&
                       performance.navigation.type === 0 &&
                       !!nav && nav.entryType === 'navigation' && nav.type === 'navigate' &&
                       nav.name === location.href &&
                       typeof nav.domComplete === 'number' &&
                       performance.getEntriesByType('resource').length === 0;
              var a = document.createElement('a'); a.setAttribute('href', ok ? '/ok' : '/bad');
              document.getElementById('t').appendChild(a);
            } catch (err) {
              var e = document.createElement('a'); e.setAttribute('href', '/err'); document.getElementById('t').appendChild(e);
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

    // An upload widget builds a File from its own bytes during init — the same unguarded construction Blob
    // is here for. It is a named Blob and must satisfy both instanceof checks.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_File_IsANamedBlob(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            try {
              var f = new File(['hello'], 'note.txt', { type: 'text/plain', lastModified: 5 });
              var ok = f instanceof File && f instanceof Blob &&
                       f.name === 'note.txt' && f.size === 5 && f.type === 'text/plain' && f.lastModified === 5;
              var a = document.createElement('a'); a.setAttribute('href', ok ? '/ok' : '/bad');
              document.getElementById('t').appendChild(a);
            } catch (err) {
              var e = document.createElement('a'); e.setAttribute('href', '/err'); document.getElementById('t').appendChild(e);
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

    // The CSS namespace object, read off the global bare — a module that did so died on the ReferenceError,
    // taking the entry point with it. escape() has to serialize an identifier a selector can be built from,
    // and a feature probe answers as the current browser this render presents itself as.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_CssNamespace_EscapesAnIdentifierAndAnswersAFeatureProbe(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="t"></div>
            <script>
            var escaped = CSS.escape('a.b');
            var probed = CSS.supports('display', 'grid') && CSS.supports('(display: grid)') && !CSS.supports('');
            var a = document.createElement('a');
            a.setAttribute('href', '/css/' + escaped + '/' + probed);
            document.getElementById('t').appendChild(a);
            </script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/css/a\\.b/true\"", rendered);
    }
}
