using Crawler.AngleSharp.Js.Abstractions;
using Crawler.AngleSharp.Js.Jint;
using Crawler.AngleSharp.Js.Models;
using Crawler.AngleSharp.Js.Rendering;
using Crawler.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace Crawler.Tests;

// Exercises the Phase 1 pure-JS DOM path (DomMode.Js) without a host: dom.js parses the shell, the inline
// script mutates the JS DOM, and the tree is serialized back to HTML — no managed DOM wrappers involved.
public class JsDomRendererTests
{
    private const string _jintEngineKey = "anglesharp-js-jint";

    private static JsRenderer CreateJsRenderer()
    {
        var services = new ServiceCollection();
        services.AddAngleSharpJintCrawler(new CrawlerOptions());
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredKeyedService<IJsEngineFactory>(_jintEngineKey);
        return new JsRenderer(factory, new JsRenderOptions { DomMode = DomMode.Js }, NullLogger.Instance);
    }

    [Fact]
    public async Task JsMode_ParsesShell_RunsInlineScript_AndSerializesMutation()
    {
        const string html = """
            <!doctype html><html><head><title>t</title></head>
            <body><a href="/static">s</a><div id="r"></div>
            <script>var a=document.createElement('a');a.setAttribute('href','/injected');a.textContent='i';document.body.appendChild(a);</script>
            </body></html>
            """;

        var renderer = CreateJsRenderer();
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("<title>t</title>", rendered);
        Assert.Contains("href=\"/static\"", rendered);
        Assert.Contains("href=\"/injected\"", rendered);
    }

    [Fact]
    public async Task JsMode_RendersInnerHtmlInjection()
    {
        const string html = """
            <html><body><div id="r"></div>
            <script>document.getElementById('r').innerHTML='<a href="/inner">i</a>';</script>
            </body></html>
            """;

        var renderer = CreateJsRenderer();
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/inner\"", rendered);
    }
}
