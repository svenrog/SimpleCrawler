using Crawler.Core;
using Crawler.Js.Abstractions;
using Crawler.Js.Jint;
using Crawler.Js.Models;
using Crawler.Js.Rendering;
using Crawler.Js.V8;
using Crawler.Tests.Helpers;
using Crawler.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace Crawler.Tests;

// A module import that resolves to a non-HTTP URI must not abort the page. A protocol-relative specifier
// (`//host/x.js`) parses as a file:// URI, and the fetcher's HttpClient.Send throws "the file scheme is not
// supported" — a raw CLR exception that escaped module evaluation and killed the whole fetch (live-repro'd on
// ewheels.com). The fetcher now skips non-HTTP schemes and treats any fetch failure as an empty module, so the
// render degrades gracefully and the rest of the page still hydrates.
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
        return new JsRenderer(factory, new JsRenderOptions(), null, NullLogger.Instance);
    }
}
