using Microsoft.Extensions.Logging;
using SimpleCrawler.Core.Extensions;
using SimpleCrawler.Js.Abstractions;
using SimpleCrawler.Js.Errors;
using SimpleCrawler.Core.Collectors;
using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Js.Models;
using SimpleCrawler.Js.Network;
using SimpleCrawler.Js.Services;
using System.Text;
using System.Text.Json;

namespace SimpleCrawler.Js.Rendering;

public sealed class JsRenderer
{
    private const int _idleTurnsBeforeSettled = 3;

    private static readonly UTF8Encoding _utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly JsExtract _emptyExtract = new(null, null, []);
    private static readonly IReadOnlyDictionary<string, JsonElement> _emptySlices = new Dictionary<string, JsonElement>();

    private readonly IJsEngineFactory _engineFactory;
    private readonly JsRenderOptions _options;
    private readonly ILogger _logger;
    private readonly string _extractScript;
    private readonly string? _collectScript;
    private readonly bool _hasCollectors;
    private readonly SourceCache _sources = new();
    private readonly RenderFetchCache _fetchCache = new();

    /// <summary>
    /// <paramref name="collectorBlock"/> is the JavaScript (from
    /// <see cref="DomScriptComposer.CollectorBlock(IReadOnlyList{IRenderedDomCollector})"/>) that runs
    /// registered DOM collectors in-page; <c>null</c> when none are registered, so the extract is the
    /// plain <c>__crawlerCollectLinks()</c> path with no added work.
    /// </summary>
    public JsRenderer(IJsEngineFactory engineFactory, JsRenderOptions options, ILogger logger, string? collectorBlock = null)
    {
        _engineFactory = engineFactory;
        _options = options;
        _logger = logger;
        _hasCollectors = collectorBlock is not null;
        _extractScript = collectorBlock is null
            ? "__crawlerCollectLinks()"
            : $"(() => {{ const out = JSON.parse(__crawlerCollectLinks()); {collectorBlock} return JSON.stringify(out); }})()";

        // The collect-only envelope deliberately does not call __crawlerCollectLinks: a consumer that only
        // wants collector slices should not pay for the anchor walk, nor receive crawl semantics it has no
        // use for. Same block, same per-collector isolation, different envelope.
        _collectScript = collectorBlock is null
            ? null
            : $"(() => {{ const out = {{}}; {collectorBlock} return JSON.stringify(out); }})()";
    }

    /// <summary>
    /// Render path (tests/diagnostics inspect the serialized HTML): a scriptless shell can't mutate
    /// anything, so it is returned verbatim; otherwise the bundle runs and the tree is serialized.
    /// </summary>
    public Task<byte[]> RenderAsync(byte[] shell, string pageUrl, HttpClient client, CancellationToken cancellationToken)
    {
        if (!ContainsScriptTag(shell))
            return Task.FromResult(shell);

        return RunAsync(shell, pageUrl, client, SerializeJs, shell, cancellationToken);
    }

    /// <summary>
    /// Crawl path: anchors/canonical/robots are read straight off the live DOM — no serialize, no
    /// AngleSharp reparse. Scriptless shells still parse, because extraction needs the tree.
    /// </summary>
    internal Task<JsExtract> ExtractAsync(byte[] shell, string pageUrl, HttpClient client, CancellationToken cancellationToken)
        => RunAsync(shell, pageUrl, client, CollectLinks, _emptyExtract, cancellationToken);

    /// <summary>
    /// Collector path: renders the page and returns only the per-collector JSON slices the registered
    /// <see cref="IRenderedDomCollector"/> fragments produced, keyed by <see cref="IDomCollector.Key"/>.
    /// Empty when no collectors were registered, or when the DOM parse aborted before a tree existed.
    /// <para>
    /// This is <see cref="ExtractAsync"/>'s surface for a consumer that is not crawling: it renders on the
    /// same engine, drains the same way, and isolates a misbehaving fragment identically, but it carries no
    /// anchors, canonical or meta-robots — the crawl-essential fields a non-crawl caller has no use for and
    /// would pay an anchor walk to receive. The renderer stays neutral about what any collector captures;
    /// the slice is opaque JSON either way.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyDictionary<string, JsonElement>> CollectAsync(byte[] shell, string pageUrl, HttpClient client, CancellationToken cancellationToken)
    {
        if (_collectScript is null)
            return _emptySlices;

        return await RunAsync(shell, pageUrl, client, CollectSlices, _emptySlices, cancellationToken);
    }

    /// <summary>
    /// The DOM lives entirely in JS (Preludes/dom.js). HTML goes in via __crawlerLoadHtml, the bundle mutates
    /// the JS DOM with no managed crossings, and timers drain through __crawlerPump. `finalize` produces the
    /// caller's result from the settled tree (serialize for rendering, collect-links for crawling); `abortValue`
    /// is returned if the DOM parse itself fails before the tree exists.
    /// </summary>
    private async Task<T> RunAsync<T>(byte[] shell, string pageUrl, HttpClient client, Func<IJsEngine, T> finalize, T abortValue, CancellationToken cancellationToken)
    {
        var totalTime = RenderProfiler.Start();

        var pageUri = new Uri(pageUrl);
        var html = _utf8NoBom.GetString(shell);

        var fetcher = new HttpModuleFetcher(client, _sources, _logger, cancellationToken);

        var createTime = RenderProfiler.Start();
        var baseEngine = _engineFactory.Create(fetcher, pageUri, cancellationToken);
        using var disposableEngine = baseEngine as IDisposable;

        RenderProfiler.Stop("phase.engineCreate", createTime);

        var engine = RenderProfiler.Enabled ? new ProfilingJsEngine(baseEngine) : baseEngine;
        var isolation = new RenderIsolation(_logger, pageUrl);
        var setupTime = RenderProfiler.Start();

        // The DOM prelude is the one crossing that is not isolated: a page whose document/window never
        // existed is not a partial render, and reporting it as one would hand the caller a page that failed
        // for our reasons dressed as a page that ran.
        if (engine.BeginPage())
        {
            try
            {
                RunPrelude(engine, JsPreludes.Dom);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "The JS DOM prelude failed on '{url}'; nothing could be rendered.", pageUrl);
                throw;
            }
        }

        isolation.Run("Location setup", () => engine.CallGlobal("__crawlerSetLocation", pageUrl));
        isolation.Run("Viewport setup", () => engine.CallGlobal("__crawlerSetViewport", (int)_options.Viewport.Width, (int)_options.Viewport.Height));

        ConfigureScriptLogging(engine, isolation);
        ConfigureDiagnostics(engine, isolation);
        if (_options.EnableWebGl)
            isolation.Run("WebGL setup", () => engine.CallGlobal("__crawlerEnableWebGl"));

        // A shim prelude that fails costs the page its shim, not its render: the base prelude's inert stubs
        // are still installed underneath each of them.
        if (_options.EnableIndexedDb)
            isolation.Run("IndexedDB prelude", () => RunPrelude(engine, JsPreludes.IndexedDb));

        if (_options.EnableStreams)
            isolation.Run("Streams prelude", () => RunPrelude(engine, JsPreludes.Stream));

        if (DomProfiler.Enabled)
            isolation.Run("DOM profile setup", () => engine.CallGlobal("__crawlerEnableDomProfile"));

        RenderProfiler.Stop("phase.setupGlobals", setupTime);

        var parseTime = RenderProfiler.Start();
        if (!isolation.Run("JS DOM parse", () => engine.CallGlobal("__crawlerLoadHtml", html)))
        {
            RenderProfiler.Stop("phase.total", totalTime);
            return abortValue;
        }
        RenderProfiler.Stop("phase.parse", parseTime);

        var documentBaseUri = ResolveDocumentBase(engine, isolation, pageUri);

        var collectTime = RenderProfiler.Start();
        var (regularScripts, moduleEntries) = await CollectScriptsFromJsAsync(engine, isolation, documentBaseUri, pageUrl, client, _sources, cancellationToken);
        RenderProfiler.Stop("phase.collect", collectTime);

        // The markup had a <script> tag but the parser surfaced nothing executable (e.g. JSON), so nothing
        // ran: finalize against the parsed-only tree.
        if (regularScripts.Count == 0 && moduleEntries.Count == 0)
        {
            return Finalize(engine, isolation, finalize, abortValue, totalTime);
        }

        // Snapshot the parsed-but-unscripted tree so a streaming/hydration bundle that tears down the
        // server markup without rebuilding it can't leave the render worse off than the shell it started
        // from. Only under EnableStreams, the one path that lets such bundles run.
        if (_options.EnableStreams)
            isolation.Run("Baseline capture", () => engine.CallGlobal("__crawlerCaptureBaseline"));

        if (_options.EnableFetch)
        {
            // Embedded as a variadic function (not a host object): ClearScript's V8 backend can't reflectively
            // invoke a host object's instance method under NativeAOT. The fetch prelude wraps this in a JS
            // __http shim. See JsHttp.requestJson.
            var http = new JsHttp(client, pageUri, _logger, _fetchCache, cancellationToken);
            isolation.Run("Fetch prelude", () =>
            {
                engine.EmbedFunction("__httpRequest", http.requestJson);
                RunPrelude(engine, JsPreludes.Fetch);
            });
        }

        var bundleExecutionTime = RenderProfiler.Start();
        foreach (var script in regularScripts)
        {
            RunRegularJs(engine, isolation, script);
        }

        foreach (var module in moduleEntries)
        {
            RunModule(engine, isolation, module);
        }
        RenderProfiler.Stop("phase.bundleExec", bundleExecutionTime);

        isolation.Run("DOMContentLoaded dispatch", () => engine.CallGlobal("__crawlerFireDomContentLoaded"));

        var drainTime = RenderProfiler.Start();
        await DrainJsAsync(engine, isolation, documentBaseUri, pageUri, client, cancellationToken);
        RenderProfiler.Stop("phase.drain", drainTime);

        if (_options.EnableStreams)
        {
            var restored = isolation.Run("Regression guard", () => engine.Evaluate<int>("__crawlerGuardRegression()"), -1);
            if (restored >= 0)
                _logger.LogDebug("JS render regressed below the server-rendered shell on '{url}'; restored baseline ({anchors} anchors).", pageUrl, restored);
        }

        return Finalize(engine, isolation, finalize, abortValue, totalTime);
    }

    /// <summary>
    /// The last crossing, and the one with the most to lose: a throw while reading the settled tree would
    /// discard a render that already ran, so it degrades to <paramref name="abortValue"/> like any other.
    /// </summary>
    private static T Finalize<T>(IJsEngine engine, RenderIsolation isolation, Func<IJsEngine, T> finalize, T abortValue, long totalTime)
    {
        var finalizeTime = RenderProfiler.Start();
        var result = isolation.Run("Render finalize", () => finalize(engine), abortValue);
        RenderProfiler.Stop("phase.finalize", finalizeTime);
        RenderProfiler.Stop("phase.total", totalTime);

        if (DomProfiler.Enabled)
            DomProfiler.Add(isolation.Run("DOM profile dump", () => engine.Evaluate<string>("__crawlerDomProfileDump()"), string.Empty));

        return result;
    }

    /// <summary>
    /// Relative script/resource URLs resolve against the document base URL (the first &lt;base href&gt;, if any),
    /// not the page URL — matching the browser. Without this, a &lt;base href="/"&gt; page served from a nested
    /// path fetches the site's HTML fallback for every relative &lt;script src&gt;, and the engine aborts on it.
    /// </summary>
    private static Uri ResolveDocumentBase(IJsEngine engine, RenderIsolation isolation, Uri pageUri)
    {
        var baseHref = isolation.Run("Base href read", () => engine.Evaluate<string>("__crawlerGetBaseHref()"), string.Empty);
        if (!string.IsNullOrEmpty(baseHref) && Uri.TryCreate(pageUri, baseHref, out var baseUri))
            return baseUri;

        return pageUri;
    }

    private async Task<(IReadOnlyList<RegularScript> Regular, IReadOnlyList<ModuleScript> Modules)> CollectScriptsFromJsAsync(IJsEngine engine, RenderIsolation isolation, Uri baseUri, string pageUrl, HttpClient client, SourceCache sources, CancellationToken cancellationToken)
    {
        var regular = new List<RegularScript>();
        var modules = new List<ModuleScript>();

        var json = isolation.Run("Script collection", () => engine.Evaluate<string>("__crawlerCollectScripts()"), string.Empty);
        if (string.IsNullOrEmpty(json))
            return (regular, modules);

        using var doc = JsonDocument.Parse(json);
        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            var isModule = entry.TryGetProperty("module", out var moduleProp) && moduleProp.ValueKind == JsonValueKind.True;
            var external = entry.TryGetProperty("external", out var externalProp) && externalProp.ValueKind == JsonValueKind.True;
            var src = entry.TryGetProperty("src", out var srcProp) && srcProp.ValueKind == JsonValueKind.String ? srcProp.GetString()! : "";
            var text = entry.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String ? textProp.GetString()! : "";

            if (external)
            {
                if (!Uri.TryCreate(baseUri, src, out var absolute))
                    continue;

                var source = await FetchSourceAsync(client, sources, absolute, cancellationToken);
                if (source is null)
                    continue;

                if (isModule)
                    modules.Add(new ModuleScript(absolute.ToString(), source, External: true));
                else
                    regular.Add(new RegularScript(source, absolute.ToString(), src, External: true));
            }
            else
            {
                if (string.IsNullOrEmpty(text))
                    continue;

                if (isModule)
                    modules.Add(new ModuleScript(InlineModuleSpecifier(pageUrl, modules.Count), text, External: false));
                else
                    regular.Add(new RegularScript(text, pageUrl, string.Empty, External: false));
            }
        }

        return (regular, modules);
    }

    /// <summary>
    /// An inline module has no URL of its own, so it borrows the page's — but a page with two of them would
    /// then register the same specifier twice, which one engine refuses outright and the other answers from
    /// its module cache, running the first module's code in place of the second. The ordinal goes in the
    /// fragment: relative imports inside the module resolve against the page URL either way.
    /// </summary>
    private static string InlineModuleSpecifier(string pageUrl, int ordinal)
        => $"{pageUrl}#inline-{ordinal}";

    private static void RunRegularJs(IJsEngine engine, RenderIsolation isolation, RegularScript script)
    {
        SetCurrentScript(engine, isolation, script.External ? script.RawSrc : "");
        try
        {
            isolation.Run("Bundle execution", () =>
            {
                if (script.External)
                    engine.ExecuteCached(script.Src, script.Source);
                else
                    engine.Execute(script.Source);
            });
        }
        finally
        {
            SetCurrentScript(engine, isolation, null);
        }
    }

    /// <summary>
    /// A classic &lt;script&gt; exposes itself as document.currentScript only while it runs synchronously; webpack's
    /// auto-public-path (and Next's instanceof-HTMLScriptElement invariant over it) reads that during chunk
    /// evaluation, so it must be set around each execution and cleared after — exactly as a browser does.
    /// <para>
    /// <paramref name="src"/> is the <c>src</c> attribute as authored, not the URL this host resolved and
    /// fetched. The synthetic node stores it as the attribute, so getAttribute("src") returns the literal
    /// string a browser would and the .src property resolves it back to absolute for webpack — feeding the
    /// resolved URL in collapses that distinction and breaks Turbopack's chunk identity (see
    /// HTMLScriptElement.src).
    /// </para>
    /// </summary>
    private static void SetCurrentScript(IJsEngine engine, RenderIsolation isolation, string? src)
        => isolation.Run("currentScript update", () => engine.CallGlobal("__crawlerSetCurrentScript", src));

    private async Task DrainJsAsync(IJsEngine engine, RenderIsolation isolation, Uri baseUri, Uri pageUri, HttpClient client, CancellationToken cancellationToken)
    {
        // Settle after a few idle turns: __crawlerPending reports the queue depth before __crawlerPump runs
        // it, so a turn with no pending work counts as idle. Runtime-appended <script src>/<link> chunks are
        // loaded at the top of each turn — before the timer pump — so a code-split route's chunk is installed
        // and its load event fired before webpack's chunk-load timeout callback runs.
        var iterations = 0;
        var idle = 0;
        while (iterations++ < _options.MaxTaskDrainIterations && idle < _idleTurnsBeforeSettled)
        {
            var loadedResource = await DrainResourcesAsync(engine, isolation, baseUri, pageUri, client, cancellationToken);

            // A turn that could not be read is a turn with nothing left to run: every fallback here settles
            // the loop rather than spinning it against an engine that is answering with exceptions.
            isolation.Run("Microtask drain", engine.RunMicrotasks);
            var pending = isolation.Run("Task queue read", () => engine.Evaluate<int>("__crawlerPending()"), 0);
            isolation.Run("Task pump", () => engine.Evaluate<int>("__crawlerPump()"), 0);
            isolation.Run("Microtask drain", engine.RunMicrotasks);

            var pendingResources = isolation.Run("Resource queue read", () => engine.Evaluate<int>("__crawlerPendingResources()"), 0);
            idle = pending == 0 && pendingResources == 0 && !loadedResource ? idle + 1 : 0;
        }
    }

    private async Task<bool> DrainResourcesAsync(IJsEngine engine, RenderIsolation isolation, Uri baseUri, Uri pageUri, HttpClient client, CancellationToken cancellationToken)
    {
        var json = isolation.Run("Resource queue take", () => engine.Evaluate<string>("__crawlerTakeResources()"), string.Empty);
        if (string.IsNullOrEmpty(json))
            return false;

        using var doc = JsonDocument.Parse(json);
        var loaded = false;
        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            var id = entry.GetProperty("id").GetInt32();
            var tag = entry.TryGetProperty("tag", out var tagProp) ? tagProp.GetString() : null;
            var src = entry.TryGetProperty("src", out var srcProp) ? srcProp.GetString() : null;
            var type = entry.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;

            await LoadResourceAsync(engine, isolation, id, tag, src, type, baseUri, pageUri, client, cancellationToken);
            loaded = true;
        }

        return loaded;
    }

    /// <summary>
    /// A same-origin &lt;script&gt; is fetched and executed so a webpack chunk's module registrations run; a &lt;link&gt;
    /// is treated as loaded without fetching (a crawl needs no CSS). Cross-origin scripts (AppInsights/GTM and
    /// similar analytics SDKs) are left pending unless
    /// <see cref="JsRenderOptions.ExecuteCrossOriginScripts"/> is set — running them is slow and yields no
    /// links, and nothing awaits their load, which is the right trade for a crawl but the wrong one when the
    /// render exists to observe what the page installs. Every other case fires the node's load (or error)
    /// event to settle the awaiting import().
    /// <para>
    /// <paramref name="type"/> is the node's <c>type</c> attribute, and splits the same two entries the
    /// initial markup is already split into: <c>module</c> goes to the module loader so its imports resolve,
    /// anything else to the classic-script entry.
    /// </para>
    /// </summary>
    private async Task LoadResourceAsync(IJsEngine engine, RenderIsolation isolation, int id, string? tag, string? src, string? type, Uri baseUri, Uri pageUri, HttpClient client, CancellationToken cancellationToken)
    {
        if (!string.Equals(tag, "script", StringComparison.Ordinal))
        {
            FireResourceEvent(engine, isolation, id, "load");
            return;
        }

        if (string.IsNullOrEmpty(src) || !Uri.TryCreate(baseUri, src, out var absolute))
        {
            FireResourceEvent(engine, isolation, id, "error");
            return;
        }

        if (!_options.ExecuteCrossOriginScripts
            && !string.Equals(absolute.Host, pageUri.Host, StringComparison.OrdinalIgnoreCase))
            return;

        var source = await FetchSourceAsync(client, _sources, absolute, cancellationToken);
        if (source is null)
        {
            FireResourceEvent(engine, isolation, id, "error");
            return;
        }

        // A module never becomes document.currentScript, in a browser or here, so only the classic path
        // brackets its execution with one.
        if (string.Equals(type, "module", StringComparison.OrdinalIgnoreCase))
        {
            RunModule(engine, isolation, new ModuleScript(absolute.AbsoluteUri, source, External: true));
            FireResourceEvent(engine, isolation, id, "load");
            return;
        }

        SetCurrentScript(engine, isolation, src);
        bool executed;
        try
        {
            executed = isolation.Run("Chunk execution", () => engine.ExecuteCached(absolute.AbsoluteUri, source));
        }
        finally
        {
            SetCurrentScript(engine, isolation, null);
        }

        FireResourceEvent(engine, isolation, id, executed ? "load" : "error");
    }

    private static void FireResourceEvent(IJsEngine engine, RenderIsolation isolation, int id, string type)
        => isolation.Run("Resource event dispatch", () => engine.CallGlobal("__crawlerFireResourceEvent", id, type));

    private JsExtract CollectLinks(IJsEngine engine)
    {
        var json = engine.Evaluate<string>(_extractScript);
        if (string.IsNullOrEmpty(json))
            return _emptyExtract;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var hrefs = new List<string?>();
        if (root.TryGetProperty("anchors", out var anchorsProp) && anchorsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in anchorsProp.EnumerateArray())
                hrefs.Add(item.ValueKind == JsonValueKind.String ? item.GetString() : null);
        }

        var canonical = root.TryGetProperty("canonical", out var canonicalProp) && canonicalProp.ValueKind == JsonValueKind.String ? canonicalProp.GetString() : null;
        var robots = root.TryGetProperty("robots", out var robotsProp) && robotsProp.ValueKind == JsonValueKind.String ? robotsProp.GetString() : null;

        var collectors = _hasCollectors ? DomScriptComposer.ReadCollectors(root) : null;

        return new JsExtract(canonical, robots, hrefs, collectors);
    }

    /// <summary>
    /// Reads the collector-only envelope produced by <c>_collectScript</c> into per-collector slices.
    /// Shares <see cref="DomScriptComposer.ReadCollectors"/> with the crawl path, so a fragment that threw
    /// or returned unserializable data is absent here for exactly the same reason it would be there.
    /// </summary>
    private IReadOnlyDictionary<string, JsonElement> CollectSlices(IJsEngine engine)
    {
        var json = engine.Evaluate<string>(_collectScript!);
        if (string.IsNullOrEmpty(json))
            return _emptySlices;

        using var doc = JsonDocument.Parse(json);
        return DomScriptComposer.ReadCollectors(doc.RootElement);
    }

    private static byte[] SerializeJs(IJsEngine engine)
    {
        var html = engine.Evaluate<string>("__crawlerSerialize()");
        return string.IsNullOrEmpty(html) ? [] : _utf8NoBom.GetBytes(html);
    }

    private static void RunPrelude(IJsEngine engine, in PreludeEntry prelude) => engine.ExecuteCached(prelude.Key, prelude.Source);

    /// <summary>
    /// The bundle's console.* calls reach the logger only when ScriptLogging opts in: the JS console stays a
    /// no-op until __crawlerSetLogLevel raises it off Infinity, so unset means no embedding and no formatting
    /// cost. The level numbers match LogLevel's, so the floor round-trips and __crawlerLog casts straight back.
    /// </summary>
    private void ConfigureScriptLogging(IJsEngine engine, RenderIsolation isolation)
    {
        if (_options.ScriptLogging is not { } level)
            return;

        isolation.Run("Script logging setup", () =>
        {
            engine.EmbedFunction("__crawlerLog", LogFromScript);
            engine.CallGlobal("__crawlerSetLogLevel", (int)level);
        });
    }

    /// <summary>
    /// Embedded unconditionally (unlike the opt-in console bridge): the task pump and resource-event loop
    /// deliberately swallow exceptions from scheduled callbacks so one bad chunk can't abort the drain, which
    /// is exactly why a fatal hydration/commit throw otherwise presents as "the render just settled" with no
    /// diagnostics. Routing those catches through this channel turns them into a named exception with a stack
    /// the moment the renderer's log level is Debug, without spamming a normal crawl by default.
    /// </summary>
    private void ConfigureDiagnostics(IJsEngine engine, RenderIsolation isolation)
        => isolation.Run("Diagnostics setup", () => engine.EmbedFunction("__crawlerDiagnostic", ReportDiagnostic));

    private object? ReportDiagnostic(params object?[] args)
    {
        if (!_logger.IsEnabled(LogLevel.Debug))
            return null;

        var message = args.Length > 0 ? args[0]?.ToString() ?? string.Empty : string.Empty;
        _logger.LogDebug("{Message}", message);
        return null;
    }

    private object? LogFromScript(params object?[] args)
    {
        var level = args.Length > 0 ? ToLogLevel(args[0]) : LogLevel.Information;
        if (!_logger.IsEnabled(level))
            return null;

        var message = args.Length > 1 ? args[1]?.ToString() ?? string.Empty : string.Empty;
        _logger.Log(level, "{Message}", message);
        return null;
    }

    private static LogLevel ToLogLevel(object? value)
    {
        var number = value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            _ => int.TryParse(value?.ToString(), out var parsed) ? parsed : (int)LogLevel.Information,
        };

        return number switch
        {
            <= 0 => LogLevel.Trace,
            1 => LogLevel.Debug,
            2 => LogLevel.Information,
            3 => LogLevel.Warning,
            _ => LogLevel.Error,
        };
    }

    private static void RunModule(IJsEngine engine, RenderIsolation isolation, ModuleScript module)
        => isolation.Run("Module execution", () => engine.EvaluateModule(module.Specifier, module.Source, module.External));

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
            if (c is >= (byte)'A' and <= (byte)'Z')
                c = (byte)(c + 32);

            if (c != lowercase[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// A script's source, or <c>null</c> when it cannot be had — for which the caller fires the node's error
    /// event, as a browser does. Nothing about one script's source is allowed to reach the top of the render:
    /// a bundle appends a script from a <c>blob:</c> URL it built (a module shim rewriting imports does), which
    /// <see cref="HttpClient"/> answers with a raw <see cref="NotSupportedException"/>, and a fetch that faults
    /// or times out is ordinary against a live page. Same reasoning and same shape as
    /// <c>HttpModuleFetcher.Download</c>, which the module half of this already had.
    /// </summary>
    private async Task<string?> FetchSourceAsync(HttpClient client, SourceCache sources, Uri absolute, CancellationToken cancellationToken)
    {
        if (sources.TryGet(absolute, out var cached))
            return cached;

        if (absolute.Scheme != Uri.UriSchemeHttp && absolute.Scheme != Uri.UriSchemeHttps)
        {
            _logger.LogWarning("Script source '{url}' is not fetchable over HTTP.", absolute);
            return sources.Store(absolute, null);
        }

        try
        {
            using var response = await client.GetAsync(absolute, cancellationToken);
            if (!response.IsSuccessStatus())
            {
                _logger.LogWarning("Script source '{url}' was refused with status {status}.", absolute, (int)response.StatusCode);
                return sources.Store(absolute, null);
            }

            return sources.Store(absolute, await response.Content.ReadAsStringAsync(cancellationToken));
        }
        // Guarded on the caller's token rather than on the exception type: a per-request timeout arrives as a
        // cancellation that is not the crawl stopping, and losing the whole render to one slow chunk is the
        // failure this exists to prevent.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Script fetch error for '{url}': {message}", absolute, ex.Message);
            return sources.Store(absolute, null);
        }
    }
}
