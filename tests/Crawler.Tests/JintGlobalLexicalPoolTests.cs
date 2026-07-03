using Crawler.Core;
using Crawler.Js.Abstractions;
using Crawler.Js.Jint;
using Crawler.Js.Models;
using Crawler.Js.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace Crawler.Tests;

// A pooled Jint engine reuses its realm across pages. A classic <script>'s top-level `let`/`const`/`class`
// binds into the realm's global lexical record, not onto the global object, so the per-page reset (which only
// drops global-object properties) can't clear it. The second page re-running the same chunk therefore used to
// throw "X has already been declared", aborting that script. BeginPage now clears the lexical record between
// pages, so the redeclaration succeeds and the script's DOM side effect runs on every page.
public class JintGlobalLexicalPoolTests
{
    [Fact]
    public async Task PooledEngine_TopLevelConstAcrossPages_DoesNotThrowAlreadyDeclared()
    {
        var renderer = CreateJintRenderer();
        using var client = new HttpClient(new NotFoundHandler());

        var first = await Render(renderer, client, "http://localhost:5000/p1");
        var second = await Render(renderer, client, "http://localhost:5000/p2");

        // The inline script declares `const charpstAR` then appends its anchor. On a reused realm the second
        // page's redeclaration must not abort the script, so both pages carry the appended anchor.
        Assert.Contains("href=\"/lexical\"", first);
        Assert.Contains("href=\"/lexical\"", second);
    }

    private static JsRenderer CreateJintRenderer()
    {
        var services = new ServiceCollection();
        services.AddJintCrawler(new CrawlerOptions());
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredKeyedService<IJsEngineFactory>("js-jint");
        return new JsRenderer(factory, new JsRenderOptions(), null, NullLogger.Instance);
    }

    private static async Task<string> Render(JsRenderer renderer, HttpClient client, string pageUrl)
    {
        var html = """
            <!doctype html><html><head></head><body>
            <script>
              const charpstAR = 1;
              const a = document.createElement('a');
              a.setAttribute('href', '/lexical');
              document.body.appendChild(a);
            </script>
            </body></html>
            """;

        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), pageUrl, client, CancellationToken.None);
        return Encoding.UTF8.GetString(result);
    }

    private sealed class NotFoundHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
    }
}
