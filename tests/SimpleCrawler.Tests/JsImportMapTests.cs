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
/// A bare module specifier resolves through the page's own <c>&lt;script type="importmap"&gt;</c> and nowhere
/// else. Resolving one as a path instead asks the target for <c>/@scope/pkg</c> — a URL it never published, so
/// the answer is its 404 page and everything the package would have registered is lost, on a page that told us
/// exactly where the package lives.
/// </summary>
[Collection("Crawler")]
public class JsImportMapTests
{
    private const string _mappedHtml = """
        <!doctype html><html><head>
        <script type="importmap">
        {
          "imports": {
            "@vendor/widget": "https://cdn.example.net/widget@1/index.js",
            "@vendor/widget/": "https://cdn.example.net/widget@1/"
          }
        }
        </script>
        </head><body>
        <script type="module">
        import "@vendor/widget";
        import "@vendor/widget/extra.js";
        </script>
        </body></html>
        """;

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task ABareSpecifier_ResolvesThroughThePagesImportMap(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var renderer = CreateRenderer(engine);
        var result = await renderer.RenderAsync(
            Encoding.UTF8.GetBytes(_mappedHtml), "https://www.example.com/", client, TestContext.Current.CancellationToken);
        var rendered = Encoding.UTF8.GetString(result);

        // The exact key, and the trailing-slash key that maps a whole subtree by appending the remainder.
        Assert.Contains("href=\"/ran/widget\"", rendered, StringComparison.Ordinal);
        Assert.Contains("href=\"/ran/extra\"", rendered, StringComparison.Ordinal);
        Assert.Contains("https://cdn.example.net/widget@1/index.js", handler.Requested);
        Assert.Contains("https://cdn.example.net/widget@1/extra.js", handler.Requested);
    }

    // A specifier no map covers is one a browser refuses to resolve at all. The render answers it an empty
    // module — the same nothing the page would have got — but the target is not asked for a path it never
    // published, and the module that imported it runs on.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task AnUnmappedBareSpecifier_IsNeverFetchedFromTheSite(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head></head><body>
            <script type="module">
            import "@vendor/absent";
            const a = document.createElement('a'); a.setAttribute('href', '/ran/importer'); document.body.appendChild(a);
            </script>
            </body></html>
            """;

        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var renderer = CreateRenderer(engine);
        var result = await renderer.RenderAsync(
            Encoding.UTF8.GetBytes(html), "https://www.example.com/", client, TestContext.Current.CancellationToken);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/ran/importer\"", rendered, StringComparison.Ordinal);
        Assert.Empty(handler.Requested);
    }

    /// <summary>Serves a module per path that reports itself, and records every URL it was asked for.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Requested { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Send(request, cancellationToken));

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            lock (Requested)
            {
                Requested.Add(uri.AbsoluteUri);
            }

            var name = uri.AbsolutePath.EndsWith("/extra.js", StringComparison.Ordinal) ? "extra" : "widget";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $"const a = document.createElement('a'); a.setAttribute('href', '/ran/{name}'); document.body.appendChild(a);"),
            };
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
