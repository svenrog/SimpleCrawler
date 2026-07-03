using Crawler.Core;
using Crawler.Js.Abstractions;
using Crawler.Js.Jint;
using Crawler.Js.Models;
using Crawler.Js.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;

namespace Crawler.Tests;

// A pooled Jint engine reuses its realm across pages, and Jint keeps evaluated ES modules in a per-realm
// registry keyed by specifier. An external module served from a stable URL (a shared _astro/webpack chunk)
// recurs on many pages, so the second page's re-registration used to throw "an item with the same key has
// already been added" straight out of the render. BeginPage now clears the module registry between pages, so
// each page re-evaluates the module against its own document. (Inline modules can't repro this — their
// specifier is the unique page URL.)
public class JintModulePoolTests
{
    [Fact]
    public async Task PooledEngine_RepeatedExternalModuleAcrossPages_ReEvaluatesWithoutDuplicateKey()
    {
        var renderer = CreateJintRenderer();
        using var client = new HttpClient(new SharedModuleHandler());

        var first = await Render(renderer, client, "http://localhost:5000/p1");
        var second = await Render(renderer, client, "http://localhost:5000/p2");

        // Each page keeps its own anchor and gets the module's anchor — the second no longer crashes and the
        // module's side effect ran against the second page's document rather than being skipped as stale.
        Assert.Contains("href=\"/p1\"", first);
        Assert.Contains("href=\"/from-module\"", first);
        Assert.Contains("href=\"/p2\"", second);
        Assert.Contains("href=\"/from-module\"", second);
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
        var path = new Uri(pageUrl).AbsolutePath;
        var html = $"""
            <!doctype html><html><head></head><body>
            <a href="{path}">self</a>
            <script type="module" src="/shared.mjs"></script>
            </body></html>
            """;

        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), pageUrl, client, CancellationToken.None);
        return Encoding.UTF8.GetString(result);
    }

    private sealed class SharedModuleHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/shared.mjs")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("const a=document.createElement('a');a.setAttribute('href','/from-module');document.body.appendChild(a);"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
