using AngleSharp.Dom;
using AngleSharp.Html;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Crawler.AngleSharp.Js.Abstractions;
using Crawler.AngleSharp.Js.Dom;
using Crawler.AngleSharp.Js.Errors;
using Crawler.AngleSharp.Js.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Crawler.AngleSharp.Js.Services;

public sealed class JsRenderer
{
    private const int _idleTurnsBeforeSettled = 3;

    private static readonly HtmlParser _parser = new();
    private static readonly UTF8Encoding _utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IJsEngineFactory _engineFactory;
    private readonly JsRenderOptions _options;
    private readonly ILogger _logger;
    private readonly SourceCache _sources = new();

    public JsRenderer(IJsEngineFactory engineFactory, JsRenderOptions options, ILogger logger)
    {
        _engineFactory = engineFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<byte[]> RenderAsync(byte[] shell, string pageUrl, HttpClient client, CancellationToken cancellationToken)
    {
        // A shell with no <script> at all cannot render anything, and the caller re-parses these same
        // bytes to extract links — so skip the parse, the engine, and the reserialize entirely.
        if (!ContainsScriptTag(shell))
            return shell;

        var pageUri = new Uri(pageUrl);
        using var stream = new MemoryStream(shell, writable: false);
        using var document = await _parser.ParseDocumentAsync(stream, cancellationToken);

        var (regularScripts, moduleEntries) = await CollectScriptsAsync(document, pageUrl, client, _sources, cancellationToken);

        // The markup had a <script>, but none were executable (e.g. JSON, importmap), so the DOM still
        // equals the shell: skip spinning up a JS engine (a fresh V8 isolate / Jint engine) and reserializing.
        if (regularScripts.Count == 0 && moduleEntries.Count == 0)
            return shell;

        var fetcher = new HttpModuleFetcher(client, _sources, cancellationToken);
        using var engine = _engineFactory.Create(fetcher, pageUri);
        var context = new DomContext(document, engine, pageUri);

        SetupGlobals(engine, context);

        foreach (var script in regularScripts)
            RunRegular(engine, context, script, pageUrl);

        foreach (var module in moduleEntries)
            RunModule(engine, module, pageUrl);

        Drain(engine, context, pageUrl);

        return Serialize(document.DocumentElement);
    }

    // Stream the rendered tree straight to UTF-8 bytes rather than materializing OuterHtml first: a
    // rendered SPA page is large enough that the intermediate string would be a per-page LOH allocation.
    private static byte[] Serialize(IElement? root)
    {
        if (root is null)
            return [];

        using var buffer = new MemoryStream();
        using (var writer = new StreamWriter(buffer, _utf8NoBom, leaveOpen: true))
            root.ToHtml(writer, HtmlMarkupFormatter.Instance);

        return buffer.ToArray();
    }

    private static void SetupGlobals(IJsEngine engine, DomContext context)
    {
        engine.EmbedHostObject("document", context.DocumentWrapper);
        engine.EmbedHostObject("location", context.Location);
        engine.EmbedHostObject("history", context.History);
        engine.EmbedHostObject("navigator", context.Navigator);
        engine.EmbedHostObject("localStorage", context.LocalStorage);
        engine.EmbedHostObject("sessionStorage", context.SessionStorage);
        engine.EmbedHostObject("crypto", context.Crypto);
        engine.EmbedHostObject("customElements", context.CustomElements);
        engine.EmbedHostObject("console", context.Console);
        engine.EmbedHostObject("performance", context.Performance);
        engine.EmbedHostType("URL", typeof(JsUrl));
        engine.EmbedHostType("IntersectionObserver", typeof(JsIntersectionObserver));
        engine.EmbedHostType("ResizeObserver", typeof(JsResizeObserver));
        engine.EmbedHostType("MutationObserver", typeof(JsMutationObserver));
        engine.EmbedHostType("Event", typeof(JsEvent));
        engine.EmbedHostType("CustomEvent", typeof(JsCustomEvent));
        engine.EmbedHostType("TextEncoder", typeof(JsTextEncoder));
        engine.EmbedHostType("TextDecoder", typeof(JsTextDecoder));

        var bridge = context.Bridge;
        engine.EmbedFunction("matchMedia", bridge.MatchMedia);
        engine.EmbedFunction("setTimeout", bridge.SetTimeout);
        engine.EmbedFunction("clearTimeout", bridge.Noop);
        engine.EmbedFunction("setInterval", bridge.SetInterval);
        engine.EmbedFunction("clearInterval", bridge.Noop);
        engine.EmbedFunction("requestAnimationFrame", bridge.RequestAnimationFrame);
        engine.EmbedFunction("cancelAnimationFrame", bridge.Noop);
        engine.EmbedFunction("queueMicrotask", bridge.QueueMicrotask);
        engine.EmbedFunction("addEventListener", bridge.Noop);
        engine.EmbedFunction("removeEventListener", bridge.Noop);
        engine.EmbedFunction("dispatchEvent", bridge.ReturnTrue);

        // The bundle reaches the DOM through window/self; both are just the global object here.
        // structuredClone has no host equivalent, but the bundle only clones plain data, so a JSON
        // round-trip stands in (guarded so a native implementation, if present, wins).
        engine.Execute(
            "var window=globalThis;var self=globalThis;" +
            "globalThis.structuredClone=globalThis.structuredClone||function(v){return v===undefined?undefined:JSON.parse(JSON.stringify(v));};");

        // HTMLElement is the one DOM global that bundles *extend* (`class X extends HTMLElement`) rather
        // than construct, and V8/ClearScript can't `class extends` a CLR host type (its host objects have
        // no JS prototype) — so unlike Event/CustomEvent above it has to be a real JS class. It is never
        // instantiated (customElements.define is a no-op), so the body is just no-op stubs.
        engine.Execute(
            "globalThis.HTMLElement=globalThis.HTMLElement||class HTMLElement{" +
            "addEventListener(){}removeEventListener(){}dispatchEvent(){return true;}attachShadow(){return this;}};" +
            "globalThis.HTMLScriptElement=globalThis.HTMLScriptElement||class HTMLScriptElement extends HTMLElement{};");
    }

    private void Drain(IJsEngine engine, DomContext context, string pageUrl)
    {
        // The bundle defers work onto setTimeout/requestAnimationFrame (drained from our queue) and
        // native promise jobs (dynamic import for lazy routes, drained at each RunMicrotasks boundary).
        // V8 resolves dynamic import() on those boundaries rather than synchronously like Jint, so we
        // keep pumping through empty turns until the queue has stayed idle for a few consecutive turns.
        var iterations = 0;
        var idle = 0;
        while (iterations++ < _options.MaxTaskDrainIterations && idle < _idleTurnsBeforeSettled)
        {
            var batch = context.TakeTasks();
            foreach (var callback in batch)
            {
                try
                {
                    engine.InvokeCallback(callback);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Task callback error on '{url}': {message}", pageUrl, ex.Message);
                }
            }

            engine.RunMicrotasks();
            idle = batch.Count == 0 && context.PendingTaskCount == 0 ? idle + 1 : 0;
        }
    }

    private void RunRegular(IJsEngine engine, DomContext context, RegularScript script, string pageUrl)
    {
        context.CurrentScript = engine.CreateScriptElement(script.Src);
        try
        {
            engine.Execute(script.Source);
        }
        catch (JsException ex)
        {
            _logger.LogWarning("Bundle execution error on '{url}': {message}", pageUrl, ex.Message);
        }
        finally
        {
            context.CurrentScript = null;
        }
    }

    private void RunModule(IJsEngine engine, ModuleScript module, string pageUrl)
    {
        try
        {
            engine.EvaluateModule(module.Specifier, module.Source);
        }
        catch (JsException ex)
        {
            _logger.LogWarning("Module execution error on '{url}': {message}", pageUrl, ex.Message);
        }
    }

    private static bool ContainsScriptTag(ReadOnlySpan<byte> html)
    {
        ReadOnlySpan<byte> marker = "<script"u8;
        var start = 0;
        while (true)
        {
            var index = html[start..].IndexOf((byte)'<');
            if (index < 0 || html.Length - (start + index) < marker.Length)
                return false;

            start += index;
            if (AsciiEqualsIgnoreCase(html.Slice(start, marker.Length), marker))
                return true;

            start++;
        }
    }

    private static bool AsciiEqualsIgnoreCase(ReadOnlySpan<byte> value, ReadOnlySpan<byte> lowercase)
    {
        for (var i = 0; i < lowercase.Length; i++)
        {
            var c = value[i];
            if (c >= 'A' && c <= 'Z')
                c = (byte)(c + 32);

            if (c != lowercase[i])
                return false;
        }

        return true;
    }

    private static async Task<(IReadOnlyList<RegularScript> Regular, IReadOnlyList<ModuleScript> Modules)> CollectScriptsAsync(IDocument document, string pageUrl, HttpClient client, SourceCache sources, CancellationToken cancellationToken)
    {
        var baseUri = new Uri(pageUrl);
        var regular = new List<RegularScript>();
        var modules = new List<ModuleScript>();

        foreach (var element in document.QuerySelectorAll("script"))
        {
            var script = (IHtmlScriptElement)element;
            var type = script.Type;
            if (!string.IsNullOrEmpty(type) && type is not "text/javascript" and not "module" and not "application/javascript")
                continue;

            var isModule = string.Equals(type, "module", StringComparison.Ordinal);
            var src = script.GetAttribute("src");

            if (string.IsNullOrEmpty(src))
            {
                if (string.IsNullOrEmpty(script.TextContent))
                    continue;

                if (isModule)
                    modules.Add(new ModuleScript(pageUrl, script.TextContent));
                else
                    regular.Add(new RegularScript(script.TextContent, pageUrl));

                continue;
            }

            var absolute = new Uri(baseUri, src);
            var source = await FetchSourceAsync(client, sources, absolute, cancellationToken);
            if (source is null)
                continue;

            if (isModule)
                modules.Add(new ModuleScript(absolute.ToString(), source));
            else
                regular.Add(new RegularScript(source, absolute.ToString()));
        }

        return (regular, modules);
    }

    private static async Task<string?> FetchSourceAsync(HttpClient client, SourceCache sources, Uri absolute, CancellationToken cancellationToken)
    {
        if (sources.TryGet(absolute, out var cached))
            return cached;

        using var response = await client.GetAsync(absolute, cancellationToken);
        var source = response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(cancellationToken) : null;
        return sources.Store(absolute, source);
    }
}
