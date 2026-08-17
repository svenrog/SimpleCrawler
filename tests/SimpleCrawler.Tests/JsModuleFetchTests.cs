using SimpleCrawler.Core;
using SimpleCrawler.Js.Abstractions;
using SimpleCrawler.Js.Jint;
using SimpleCrawler.Js.Models;
using SimpleCrawler.Js.Rendering;
using SimpleCrawler.Js.V8;
using SimpleCrawler.Tests.Helpers;
using SimpleCrawler.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;

namespace SimpleCrawler.Tests;

/// <summary>
/// A module import that resolves to a non-HTTP URI must not abort the page. A protocol-relative specifier
/// (`//host/x.js`) parses as a file:// URI, and the fetcher's HttpClient.Send throws "the file scheme is not
/// supported" — a raw CLR exception that escaped module evaluation and killed the whole fetch (live-repro'd on
/// a real site). The fetcher now skips non-HTTP schemes and treats any fetch failure as an empty module, so the
/// render degrades gracefully and the rest of the page still hydrates.
/// </summary>
[Collection("Crawler")]
public class JsModuleFetchTests
{
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task ModuleImportingNonHttpScheme_DegradesInsteadOfCrashing(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head><body>
            <a href="/self">s</a>
            <script type="module">
            import "//cdn.example.com/missing-chunk.js";
            const a = document.createElement('a'); a.setAttribute('href', '/module-ran'); document.body.appendChild(a);
            </script>
            </body></html>
            """;

        var renderer = CreateRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://www.example.com/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/self\"", rendered);
        Assert.Contains("href=\"/module-ran\"", rendered);
    }

    // An external module whose URL serves the site's HTML catch-all instead of JS can't be parsed as a module —
    // Jint's PrepareModule threw "Unexpected token <" from inside the loader, a raw CLR exception that killed the
    // whole fetch (live-repro'd on a real site). The module degrades to an empty module now, so the page's own
    // static content still renders instead of the crawl aborting on the page.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task ExternalModuleReturningHtmlFallback_DegradesInsteadOfCrashing(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head><body>
            <a href="/self">s</a>
            <script type="module" src="/entry.js"></script>
            </body></html>
            """;

        using var client = new HttpClient(new HtmlFallbackHandler());
        var renderer = CreateRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://www.example.com/", client, CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/self\"", rendered);
    }

    // Two inline modules on one page. They used to share a specifier — the page URL — which Jint refuses
    // outright ("an item with the same key has already been added", taking both) and V8 answers from its
    // module cache, running the first block's code again in place of the second. Each block gets its own
    // ordinal now, so both run exactly once.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task TwoInlineModules_BothRun(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head><body>
            <script type="module">
            const a = document.createElement('a'); a.setAttribute('href', '/first'); document.body.appendChild(a);
            </script>
            <script type="module">
            const b = document.createElement('a'); b.setAttribute('href', '/second'); document.body.appendChild(b);
            </script>
            </body></html>
            """;

        var renderer = CreateRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://www.example.com/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/first\"", rendered);
        Assert.Contains("href=\"/second\"", rendered);
    }

    // A module appended at runtime (a loader injecting its entry point) went to the classic-script entry
    // regardless of its type attribute, so its imports never resolved. The initial markup was already split
    // correctly by type; this is the same split on the runtime path.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task AppendedModuleScript_RunsThroughTheModuleLoader(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head><body>
            <script>
            var s = document.createElement('script');
            s.type = 'module';
            s.src = '/entry.mjs';
            document.head.appendChild(s);
            </script>
            </body></html>
            """;

        using var client = new HttpClient(new ModuleChunkHandler());
        var renderer = CreateRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://www.example.com/", client, CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        // The entry's own anchor proves it ran; the dependency's proves the import resolved, which is what
        // the classic-script entry could not do.
        Assert.Contains("href=\"/entry-ran\"", rendered);
        Assert.Contains("href=\"/dependency-ran\"", rendered);
    }

    // import.meta.url, which a loader reads to find where its own chunks live: `new URL(import.meta.url)`
    // over an undefined value throws before it fetches any of them, costing the page every component that
    // entry point defines. Jint only — it implements the syntax and delegates the properties to its host,
    // which is ours to supply, and it is the engine the published binary runs. Measured on ClearScript 7.5.1
    // while writing this: V8 answers undefined for a seeded module, a loaded one and an inline one alike, so
    // asserting it over both engines would pin a gap rather than a contract.
    [Fact]
    public async Task ImportMeta_CarriesTheModulesOwnUrl()
    {
        const string html = """
            <!doctype html><html><head></head><body>
            <script type="module" src="/assets/loader.mjs"></script>
            <script type="module">
            const a = document.createElement('a');
            // The inline module borrows the page's URL and an ordinal to tell two of them apart, so what it
            // owes a page is the page it is in — not a spelling this test would then hold the ordinal to.
            a.setAttribute('href', 'inline:' + (String(import.meta.url).indexOf('https://www.example.com/') === 0));
            document.body.appendChild(a);
            </script>
            </body></html>
            """;

        using var client = new HttpClient(new ImportMetaHandler());
        var renderer = CreateRenderer(JsEngine.Jint);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "https://www.example.com/", client, CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        // The entry module's own URL, and the imported one's — the two arrive by different routes, the entry
        // seeded under the specifier the renderer fetched it as and the dependency built by the module loader.
        Assert.Contains("href=\"module:https://www.example.com/assets/loader.mjs\"", rendered);
        Assert.Contains("href=\"dep:https://www.example.com/assets/dep.mjs\"", rendered);
        Assert.Contains("href=\"inline:true\"", rendered);
    }

    /// <summary>Serves an entry module that imports a second one; each reports its own <c>import.meta.url</c>.</summary>
    private sealed class ImportMetaHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Send(request, cancellationToken));

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.RequestUri!.AbsolutePath == "/assets/loader.mjs"
                ? """
                  import "./dep.mjs";
                  const a = document.createElement('a');
                  a.setAttribute('href', 'module:' + String(import.meta.url));
                  document.body.appendChild(a);
                  """
                : """
                  const d = document.createElement('a');
                  d.setAttribute('href', 'dep:' + String(import.meta.url));
                  document.body.appendChild(d);
                  """;

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        }
    }

    /// <summary>Serves an ES module entry point that imports a second module; both append an anchor.</summary>
    private sealed class ModuleChunkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Send(request, cancellationToken));

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.RequestUri!.AbsolutePath == "/entry.mjs"
                ? """
                  import "./dependency.mjs";
                  const a = document.createElement('a'); a.setAttribute('href', '/entry-ran'); document.body.appendChild(a);
                  """
                : """
                  const b = document.createElement('a'); b.setAttribute('href', '/dependency-ran'); document.body.appendChild(b);
                  """;

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        }
    }

    private sealed class HtmlFallbackHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // The SPA catch-all: every unmatched path (including a mis-resolved module URL) serves index.html.
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<!doctype html><html><body>not javascript</body></html>"),
            });
        }
    }

    private static JsRenderer CreateRenderer(JsEngine engine)
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
}
