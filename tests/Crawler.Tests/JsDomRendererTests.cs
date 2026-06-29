using Crawler.AngleSharp.Js.Abstractions;
using Crawler.AngleSharp.Js.Jint;
using Crawler.AngleSharp.Js.Models;
using Crawler.AngleSharp.Js.Rendering;
using Crawler.AngleSharp.Js.V8;
using Crawler.Core;
using Crawler.Tests.Helpers;
using Crawler.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace Crawler.Tests;

// Exercises the pure-JS DOM path (DomMode.Js) without a host: dom.js parses the shell, the inline
// script mutates the JS DOM, and the tree is serialized back to HTML — no managed DOM wrappers involved.
// Theory'd over both engines since the JS DOM is the single code path for Jint + V8.
public class JsDomRendererTests
{
    private static JsRenderer CreateJsRenderer(JsEngine engine)
    {
        var services = new ServiceCollection();
        var key = engine == JsEngine.V8 ? "anglesharp-js-v8" : "anglesharp-js-jint";
        if (engine == JsEngine.V8)
            services.AddAngleSharpV8Crawler(new CrawlerOptions());
        else
            services.AddAngleSharpJintCrawler(new CrawlerOptions());
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredKeyedService<IJsEngineFactory>(key);
        return new JsRenderer(factory, new JsRenderOptions { DomMode = DomMode.Js }, NullLogger.Instance);
    }

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_ParsesShell_RunsInlineScript_AndSerializesMutation(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <!doctype html><html><head><title>t</title></head>
            <body><a href="/static">s</a><div id="r"></div>
            <script>var a=document.createElement('a');a.setAttribute('href','/injected');a.textContent='i';document.body.appendChild(a);</script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("<title>t</title>", rendered);
        Assert.Contains("href=\"/static\"", rendered);
        Assert.Contains("href=\"/injected\"", rendered);
    }

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_RendersInnerHtmlInjection(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        const string html = """
            <html><body><div id="r"></div>
            <script>document.getElementById('r').innerHTML='<a href="/inner">i</a>';</script>
            </body></html>
            """;

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(html), "http://localhost:5000/", new HttpClient(), CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(result);

        Assert.Contains("href=\"/inner\"", rendered);
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
}
