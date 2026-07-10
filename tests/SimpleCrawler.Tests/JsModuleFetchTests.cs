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
