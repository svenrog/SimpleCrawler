using AngleSharp.Dom;
using AngleSharp.Html;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Crawler.AngleSharp.Js.Abstractions;
using Crawler.AngleSharp.Js.Dom;
using Crawler.AngleSharp.Js.Dom.Network;
using Crawler.AngleSharp.Js.Dom.Observers;
using Crawler.AngleSharp.Js.Dom.Window;
using Crawler.AngleSharp.Js.Errors;
using Crawler.AngleSharp.Js.Models;
using Crawler.AngleSharp.Js.Services;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Crawler.AngleSharp.Js.Rendering;

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
        var context = new DomContext(document, engine, pageUri, _options);

        if (_options.EnableFetch)
            engine.EmbedHostObject("__http", new JsHttp(client, pageUri, _logger, cancellationToken));

        SetupGlobals(engine, context, _options.EnableFetch);

        foreach (var script in regularScripts)
            RunRegular(engine, context, script, pageUrl);

        foreach (var module in moduleEntries)
            RunModule(engine, module, pageUrl);

        Drain(engine, context, pageUri, client, pageUrl, cancellationToken);

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

    private static void SetupGlobals(IJsEngine engine, DomContext context, bool enableFetch)
    {
        engine.EmbedHostObject("document", context.DocumentWrapper);
        engine.EmbedHostObject("location", context.Location);
        engine.EmbedHostObject("__history", context.History);
        engine.EmbedHostObject("navigator", context.Navigator);
        engine.EmbedHostObject("localStorage", context.LocalStorage);
        engine.EmbedHostObject("sessionStorage", context.SessionStorage);
        engine.EmbedHostObject("__crypto", context.Crypto);
        engine.EmbedHostObject("customElements", context.CustomElements);
        engine.EmbedHostObject("console", context.Console);
        engine.EmbedHostObject("performance", context.Performance);
        engine.EmbedHostType("IntersectionObserver", typeof(JsIntersectionObserver));
        engine.EmbedHostType("ResizeObserver", typeof(JsResizeObserver));
        engine.EmbedHostType("MutationObserver", typeof(JsMutationObserver));
        engine.EmbedHostType("TextEncoder", typeof(JsTextEncoder));
        engine.EmbedHostType("TextDecoder", typeof(JsTextDecoder));

        // A CLR host type embedded with AddHostType has no JS .prototype, so `x instanceof Element` throws
        // on V8 ("Function has non-object prototype undefined") rather than testing the wrapper's type.
        // So URL/Event (constructed) are embedded privately and re-exposed as JS shims that construct via
        // the host type and carry a Symbol.hasInstance, while Node/Element/Text/Document (only ever an
        // instanceof right-hand side, never `new`'d) are plain JS shims. __isInstance answers by CLR type.
        engine.EmbedHostType("__ctor_URL", typeof(JsUrl));
        engine.EmbedHostType("__ctor_Event", typeof(JsEvent));
        engine.EmbedHostType("__ctor_CustomEvent", typeof(JsCustomEvent));

        var bridge = context.Bridge;
        EmbedGlobalFunction(engine, "__isInstance", bridge.IsInstance);
        engine.Execute(JsPreludes.InstanceShims);

        EmbedGlobalFunction(engine, "matchMedia", bridge.MatchMedia);
        EmbedGlobalFunction(engine, "getComputedStyle", bridge.GetComputedStyle);
        EmbedGlobalFunction(engine, "setTimeout", bridge.SetTimeout);
        EmbedGlobalFunction(engine, "clearTimeout", bridge.Noop);
        EmbedGlobalFunction(engine, "setInterval", bridge.SetInterval);
        EmbedGlobalFunction(engine, "clearInterval", bridge.Noop);
        EmbedGlobalFunction(engine, "requestAnimationFrame", bridge.RequestAnimationFrame);
        EmbedGlobalFunction(engine, "cancelAnimationFrame", bridge.Noop);
        EmbedGlobalFunction(engine, "queueMicrotask", bridge.QueueMicrotask);
        EmbedGlobalFunction(engine, "addEventListener", bridge.Noop);
        EmbedGlobalFunction(engine, "removeEventListener", bridge.Noop);
        EmbedGlobalFunction(engine, "dispatchEvent", bridge.ReturnTrue);

        engine.Execute(JsPreludes.Global);

        // document.defaultView must return the same `window` the bundle reads through globalThis: history
        // libraries default `let {window = document.defaultView} = opts` then read window.history, so a null
        // defaultView crashes them with "Cannot read properties of null (reading 'history')".
        context.Window = engine.GetGlobalObject();

        engine.Execute(JsPreludes.Crypto);
        engine.Execute(JsPreludes.ResourceEvent);
        engine.Execute(JsPreludes.MessageChannel);
        engine.Execute(JsPreludes.History);
        engine.Execute(JsPreludes.HtmlElement);
        engine.Execute(JsPreludes.DomGlobals);

        if (enableFetch)
        {
            // AbortController/AbortSignal are plain no-op host objects (a synchronous render never aborts);
            // the rest of the surface stays in JS because it must return native Promises, read arbitrary
            // JS init objects, or invoke JS callbacks — none of which a host object does identically across
            // Jint and ClearScript.
            engine.EmbedHostType("AbortController", typeof(JsAbortController));
            engine.EmbedHostType("AbortSignal", typeof(JsAbortSignal));
            engine.Execute(JsPreludes.Fetch);
        }
    }

    // ClearScript/V8 exposes an embedded host delegate as a non-writable global that is NOT a real JS
    // Function — it has no .bind/.call (React's scheduler does `requestAnimationFrame.bind(...)`), and a
    // bundle's `globalThis.x = wrapper` reassignment silently fails. So embed the delegate under a private
    // name and expose the public global as a writable, real JS function that spreads into it. (Jint's
    // delegate wrapper already is a JS function, but the indirection is harmless there.)
    private static void EmbedGlobalFunction(IJsEngine engine, string name, VFunc function)
    {
        engine.EmbedFunction("__fn_" + name, function);
        engine.Execute($"globalThis.{name}=function(){{return __fn_{name}(...arguments);}};");
    }

    private void Drain(IJsEngine engine, DomContext context, Uri pageUri, HttpClient client, string pageUrl, CancellationToken cancellationToken)
    {
        // The bundle defers work onto setTimeout/requestAnimationFrame (drained from our queue), native
        // promise jobs (dynamic import for lazy routes, drained at each RunMicrotasks boundary), and
        // <script src> chunks it appends to the DOM (fetched and executed here so their module
        // registrations run and the awaiting import() resolves). V8 resolves dynamic import() on those
        // boundaries rather than synchronously like Jint, so we keep pumping through empty turns until
        // the queue has stayed idle for a few consecutive turns.
        var iterations = 0;
        var idle = 0;
        while (iterations++ < _options.MaxTaskDrainIterations && idle < _idleTurnsBeforeSettled)
        {
            var resources = context.TakePendingResources();
            foreach (var resource in resources)
                LoadResource(engine, context, resource, pageUri, client, pageUrl, cancellationToken);

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
            idle = resources.Count == 0 && batch.Count == 0 && context.PendingTaskCount == 0 && context.PendingResourceCount == 0
                ? idle + 1
                : 0;
        }
    }

    // A <script>/<link> the bundle appended at runtime. Same-origin scripts are fetched and executed (so a
    // webpack chunk's module registrations run); a <link> is treated as loaded without fetching, since a
    // crawl needs no CSS. Either way the resource's load event fires to settle the awaiting import(). The
    // cross-origin scripts (AppInsights/GTM/Flowbox) are analytics SDKs irrelevant to a crawl, so they are
    // left pending — running them would be slow and produce no links.
    private void LoadResource(IJsEngine engine, DomContext context, IElement resource, Uri pageUri, HttpClient client, string pageUrl, CancellationToken cancellationToken)
    {
        if (resource is IHtmlLinkElement)
        {
            FireResourceEvent(engine, context, resource, "onload");
            return;
        }

        var src = resource.GetAttribute("src");
        if (string.IsNullOrEmpty(src) || !Uri.TryCreate(pageUri, src, out var absolute))
            return;

        if (!string.Equals(absolute.Host, pageUri.Host, StringComparison.OrdinalIgnoreCase))
            return;

        var source = FetchSourceAsync(client, _sources, absolute, cancellationToken).GetAwaiter().GetResult();
        if (source is null)
        {
            FireResourceEvent(engine, context, resource, "onerror");
            return;
        }

        try
        {
            engine.Execute(source);
        }
        catch (JsException ex)
        {
            _logger.LogWarning("Chunk execution error on '{url}': {message}", pageUrl, ex.Message);
            FireResourceEvent(engine, context, resource, "onerror");
            return;
        }

        FireResourceEvent(engine, context, resource, "onload");
    }

    // webpack/React resolve a chunk's CSS (and script) promise from the resource's load event, checking
    // event.type === 'load'; the handler is assigned to the node (onload/onerror) and kept in the per-node
    // expando table (JsElement exposes those two as real properties so the assignment lands in the table on
    // both engines). __invokeResourceEvent builds the event and calls it. The chunk's own push() settles the
    // JS half, but the CSS half only settles here — without it a code-split route's import() never resolves.
    private static void FireResourceEvent(IJsEngine engine, DomContext context, IElement resource, string handler)
    {
        if (context.TryGetExpando(resource, handler, out var callback) && callback is not null)
        {
            try
            {
                engine.CallGlobal("__invokeResourceEvent", callback, handler == "onload" ? "load" : "error");
            }
            catch
            {
                // A missing/odd handler must not abort the drain; the chunk itself already executed.
            }
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
            _logger.LogWarning("Bundle execution error on '{url}': {message}\n{details}", pageUrl, ex.Message, ex.ErrorDetails);
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
