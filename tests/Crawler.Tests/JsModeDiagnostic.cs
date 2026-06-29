using Crawler.AngleSharp.Js.Abstractions;
using Crawler.AngleSharp.Js.Jint;
using Crawler.AngleSharp.Js.Models;
using Crawler.AngleSharp.Js.Rendering;
using Crawler.AngleSharp.Js.V8;
using Crawler.Core;
using Crawler.Tests.Helpers;
using Crawler.Tests.Models;
using Crawler.TestHost.Infrastructure.Factories;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Crawler.Tests;

// TEMPORARY Phase-5 diagnostic: render one framework shell through DomMode.Js and dump the result + JS errors.
public class JsModeDiagnostic
{
    private sealed class CapturingLogger : ILogger
    {
        public readonly List<string> Messages = [];
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }

    private sealed class LoggingHandler : DelegatingHandler
    {
        public readonly List<string> Requests = [];
        public LoggingHandler() : base(new HttpClientHandler()) { }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            Requests.Add($"{(int)response.StatusCode} {request.RequestUri!.PathAndQuery}");
            return response;
        }
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = base.Send(request, cancellationToken);
            Requests.Add($"{(int)response.StatusCode} {request.RequestUri!.PathAndQuery}");
            return response;
        }
    }

    [Theory]
    [InlineData(JsEngine.Jint, DomMode.Js)]
    [InlineData(JsEngine.Jint, DomMode.Bridge)]
    [InlineData(JsEngine.V8, DomMode.Js)]
    [InlineData(JsEngine.V8, DomMode.Bridge)]
    public async Task VueBindingProbe(JsEngine engine, DomMode mode)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var host = $"http://localhost:6402/";
        var app = SpaWebApplicationFactory.Create(host, "vue");
        await app.StartAsync();
        try
        {
            var services = new ServiceCollection();
            var options = new CrawlerOptions { RespectRobotsTxt = false, RespectMetaRobots = false };
            if (engine == JsEngine.V8) services.AddAngleSharpV8Crawler(options); else services.AddAngleSharpJintCrawler(options);
            var provider = services.BuildServiceProvider();
            var key = engine == JsEngine.V8 ? "anglesharp-js-v8" : "anglesharp-js-jint";
            var factory = provider.GetRequiredKeyedService<IJsEngineFactory>(key);
            var logger = new CapturingLogger();
            var renderer = new JsRenderer(factory, new JsRenderOptions { DomMode = mode }, logger);

            using var client = new HttpClient();
            // Import vue's runtime-core in isolation and report what its createRenderer export (y) resolves to,
            // plus the full namespace key/type map, to see whether the binding itself is broken under Jint.
            var probe = "<html><body><div id=\"x\"></div><script type=\"module\">" +
                "import('/_astro/runtime-core.esm-bundler.CmoS_6KH.js')" +
                ".then(function(RC){document.getElementById('x').setAttribute('data-probe','y='+typeof RC.y);})" +
                ".catch(function(e){document.getElementById('x').setAttribute('data-probe','ERR:'+(e&&e.stack?e.stack:String(e)));});" +
                "</script></body></html>";
            var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(probe), host, client, CancellationToken.None);
            var rendered = Encoding.UTF8.GetString(result);
            var m = System.Text.RegularExpressions.Regex.Match(rendered, "data-probe=\"([^\"]*)\"");
            var sb = new StringBuilder();
            sb.AppendLine($"=== vue/{engine}/{mode} binding probe ===");
            sb.AppendLine("PROBE: " + m.Groups[1].Value);
            foreach (var msg in logger.Messages) sb.AppendLine("LOG: " + msg);
            throw new Exception(sb.ToString());
        }
        finally { await app.StopAsync(); await app.DisposeAsync(); }
    }

    [Theory]
    [InlineData("react", JsEngine.V8)]
    [InlineData("preact", JsEngine.V8)]
    [InlineData("vue", JsEngine.V8)]
    [InlineData("svelte", JsEngine.V8)]
    [InlineData("solid", JsEngine.V8)]
    public async Task ImportProbe(string framework, JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var host = $"http://localhost:6401/";
        var app = SpaWebApplicationFactory.Create(host, framework);
        await app.StartAsync();
        try
        {
            var services = new ServiceCollection();
            var options = new CrawlerOptions { RespectRobotsTxt = false, RespectMetaRobots = false };
            if (engine == JsEngine.V8) services.AddAngleSharpV8Crawler(options); else services.AddAngleSharpJintCrawler(options);
            var provider = services.BuildServiceProvider();
            var key = engine == JsEngine.V8 ? "anglesharp-js-v8" : "anglesharp-js-jint";
            var factory = provider.GetRequiredKeyedService<IJsEngineFactory>(key);
            var logger = new CapturingLogger();
            var renderer = new JsRenderer(factory, new JsRenderOptions { DomMode = DomMode.Js }, logger);

            using var client = new HttpClient();
            var shellText = await client.GetStringAsync(host);
            var compUrl = System.Text.RegularExpressions.Regex.Match(shellText, "component-url=\"([^\"]+)\"").Groups[1].Value;
            var rendUrl = System.Text.RegularExpressions.Regex.Match(shellText, "renderer-url=\"([^\"]+)\"").Groups[1].Value;

            var probe = $"<html><body><astro-island id=\"x\" ssr><div id=\"inner\"></div></astro-island><script type=\"module\">" +
                $"import R from '{rendUrl}';import C from '{compUrl}';" +
                $"const el=document.getElementById('x');" +
                $"(async()=>{{try{{const h=R(el);await h(C,{{}},{{}},{{client:'only'}});" +
                $"for(let k=0;k<50;k++){{__crawlerPump();}}" +
                $"el.setAttribute('data-ok','MOUNTED inner='+el.innerHTML.length+' a='+el.querySelectorAll('a').length);}}" +
                $"catch(e){{el.setAttribute('data-ok','THREW:'+(e&&e.stack?e.stack:String(e)));}}}})();" +
                $"</script></body></html>";
            var result = await renderer.RenderAsync(Encoding.UTF8.GetBytes(probe), host, client, CancellationToken.None);
            var rendered = Encoding.UTF8.GetString(result);

            var sb = new StringBuilder();
            sb.AppendLine($"=== {framework}/{engine} probe ===");
            sb.AppendLine("OK-ATTR: " + System.Text.RegularExpressions.Regex.Match(rendered, "data-ok=\"([^\"]*)\"", System.Text.RegularExpressions.RegexOptions.Singleline).Groups[1].Value);
            var te = System.Text.RegularExpressions.Regex.Match(rendered, "<!--TASKERR(.*?)-->", System.Text.RegularExpressions.RegexOptions.Singleline);
            sb.AppendLine("TASKERR: " + (te.Success ? te.Groups[1].Value : "(none)"));
            sb.AppendLine("X-INNER: " + System.Text.RegularExpressions.Regex.Match(rendered, "id=\"x\"[^>]*>(.*?)</div>", System.Text.RegularExpressions.RegexOptions.Singleline).Groups[1].Value);
            foreach (var m in logger.Messages) sb.AppendLine("LOG: " + m);
            throw new Exception(sb.ToString());
        }
        finally { await app.StopAsync(); await app.DisposeAsync(); }
    }

    [Theory]
    [InlineData("vue", JsEngine.V8, DomMode.Js)]
    [InlineData("vue", JsEngine.Jint, DomMode.Js)]
    [InlineData("svelte", JsEngine.V8, DomMode.Js)]
    [InlineData("svelte", JsEngine.Jint, DomMode.Js)]
    public async Task Dump(string framework, JsEngine engine, DomMode mode)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var host = $"http://localhost:6400/";
        var app = SpaWebApplicationFactory.Create(host, framework);
        await app.StartAsync();

        try
        {
            var services = new ServiceCollection();
            var options = new CrawlerOptions { RespectRobotsTxt = false, RespectMetaRobots = false };
            if (engine == JsEngine.V8)
                services.AddAngleSharpV8Crawler(options);
            else
                services.AddAngleSharpJintCrawler(options);
            var provider = services.BuildServiceProvider();
            var key = engine == JsEngine.V8 ? "anglesharp-js-v8" : "anglesharp-js-jint";
            var factory = provider.GetRequiredKeyedService<IJsEngineFactory>(key);

            var logger = new CapturingLogger();
            var renderer = new JsRenderer(factory, new JsRenderOptions { DomMode = mode }, logger);

            var handler = new LoggingHandler();
            using var client = new HttpClient(handler);
            var shellText = await client.GetStringAsync(host);

            const string capture = "<script>function __cap(p,a){var s=[];for(var i=0;i<a.length;i++){var x=a[i];s.push(x&&x.stack?((x.name||'')+': '+(x.message||'')+'\\n'+x.stack):String(x));}" +
                "document.body.setAttribute('data-cap',(document.body.getAttribute('data-cap')||'')+'||'+p+':'+s.join(' '));}" +
                "globalThis.console={log:function(){},info:function(){},debug:function(){}," +
                "warn:function(){__cap('W',arguments);},error:function(){__cap('E',arguments);}};" +
                "window.onerror=function(m,u,l,c,e){__cap('O',[e||m]);};</script>";
            shellText = shellText.Replace("<body>", "<body>" + capture);
            var shell = Encoding.UTF8.GetBytes(shellText);

            var result = await renderer.RenderAsync(shell, host, client, CancellationToken.None);
            var rendered = Encoding.UTF8.GetString(result);

            var anchors = System.Text.RegularExpressions.Regex.Matches(rendered, "<a ").Count;
            var path = $@"C:\Users\svene\AppData\Local\Temp\claude\D--Projects-CSharp-SimpleCrawler\f7f00c27-c292-4026-9f27-8044de815c29\scratchpad\render-{framework}-{engine}-{mode}.html";
            await File.WriteAllTextAsync(path, rendered);

            var sb = new StringBuilder();
            sb.AppendLine($"=== {framework}/{engine}/{mode}: {rendered.Length} bytes, {anchors} anchors ===");
            foreach (var r in handler.Requests)
                sb.AppendLine("HTTP: " + r);
            foreach (var m in logger.Messages)
                sb.AppendLine("LOG: " + m);
            throw new Exception(sb.ToString());
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
