using Microsoft.Extensions.Logging;
using SimpleCrawler.Core.Extensions;
using SimpleCrawler.Js.Abstractions;
using SimpleCrawler.Js.Errors;
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

    private readonly IJsEngineFactory _engineFactory;
    private readonly JsRenderOptions _options;
    private readonly ILogger _logger;
    private readonly SourceCache _sources = new();
    private readonly RenderFetchCache _fetchCache = new();

    public JsRenderer(IJsEngineFactory engineFactory, JsRenderOptions options, ILogger logger)
    {
        _engineFactory = engineFactory;
        _options = options;
        _logger = logger;
    }

    // Render path (tests/diagnostics inspect the serialized HTML): a scriptless shell can't mutate
    // anything, so it is returned verbatim; otherwise the bundle runs and the tree is serialized.
    public Task<byte[]> RenderAsync(byte[] shell, string pageUrl, HttpClient client, CancellationToken cancellationToken)
    {
        if (!ContainsScriptTag(shell))
            return Task.FromResult(shell);

        return RunAsync(shell, pageUrl, client, SerializeJs, shell, cancellationToken);
    }

    // Crawl path: anchors/canonical/robots are read straight off the live DOM — no serialize, no
    // AngleSharp reparse. Scriptless shells still parse, because extraction needs the tree.
    internal Task<JsExtract> ExtractAsync(byte[] shell, string pageUrl, HttpClient client, CancellationToken cancellationToken)
        => RunAsync(shell, pageUrl, client, CollectLinks, _emptyExtract, cancellationToken);

    // The DOM lives entirely in JS (Preludes/dom.js). HTML goes in via __crawlerLoadHtml, the bundle mutates
    // the JS DOM with no managed crossings, and timers drain through __crawlerPump. `finalize` produces the
    // caller's result from the settled tree (serialize for rendering, collect-links for crawling); `abortValue`
    // is returned if the DOM parse itself fails before the tree exists.
    private async Task<T> RunAsync<T>(byte[] shell, string pageUrl, HttpClient client, Func<IJsEngine, T> finalize, T abortValue, CancellationToken cancellationToken)
    {
        var totalTime = RenderProfiler.Start();

        var pageUri = new Uri(pageUrl);
        var html = _utf8NoBom.GetString(shell);

        var fetcher = new HttpModuleFetcher(client, _sources, cancellationToken);

        var createTime = RenderProfiler.Start();
        var baseEngine = _engineFactory.Create(fetcher, pageUri);
        using var disposableEngine = baseEngine as IDisposable;

        RenderProfiler.Stop("phase.engineCreate", createTime);

        var engine = RenderProfiler.Enabled ? new ProfilingJsEngine(baseEngine) : baseEngine;
        var setupTime = RenderProfiler.Start();

        if (engine.BeginPage())
            RunPrelude(engine, JsPreludes.Dom);

        engine.CallGlobal("__crawlerSetLocation", pageUrl);
        engine.CallGlobal("__crawlerSetViewport", (int)_options.Viewport.Width, (int)_options.Viewport.Height);

        ConfigureScriptLogging(engine);
        ConfigureDiagnostics(engine);
        if (_options.EnableIndexedDb)
            RunPrelude(engine, JsPreludes.IndexedDb);

        if (_options.EnableStreams)
            RunPrelude(engine, JsPreludes.Stream);

        if (DomProfiler.Enabled)
            engine.CallGlobal("__crawlerEnableDomProfile");

        RenderProfiler.Stop("phase.setupGlobals", setupTime);

        var parseTime = RenderProfiler.Start();
        try
        {
            engine.CallGlobal("__crawlerLoadHtml", html);
        }
        catch (JsException ex)
        {
            _logger.LogWarning("JS DOM parse error on '{url}': {message}\n{details}", pageUrl, ex.Message, ex.ErrorDetails);
            RenderProfiler.Stop("phase.total", totalTime);
            return abortValue;
        }
        RenderProfiler.Stop("phase.parse", parseTime);

        var documentBaseUri = ResolveDocumentBase(engine, pageUri);

        var collectTime = RenderProfiler.Start();
        var (regularScripts, moduleEntries) = await CollectScriptsFromJsAsync(engine, documentBaseUri, pageUrl, client, _sources, cancellationToken);
        RenderProfiler.Stop("phase.collect", collectTime);

        // The markup had a <script> tag but the parser surfaced nothing executable (e.g. JSON), so nothing
        // ran: finalize against the parsed-only tree.
        if (regularScripts.Count == 0 && moduleEntries.Count == 0)
        {
            return Finalize(engine, finalize, totalTime);
        }

        // Snapshot the parsed-but-unscripted tree so a streaming/hydration bundle that tears down the
        // server markup without rebuilding it can't leave the render worse off than the shell it started
        // from. Only under EnableStreams, the one path that lets such bundles run.
        if (_options.EnableStreams)
            engine.CallGlobal("__crawlerCaptureBaseline");

        if (_options.EnableFetch)
        {
            engine.EmbedHostObject("__http", new JsHttp(client, pageUri, _logger, _fetchCache, cancellationToken));
            RunPrelude(engine, JsPreludes.Fetch);
        }

        var bundleExecutionTime = RenderProfiler.Start();
        foreach (var script in regularScripts)
        {
            RunRegularJs(engine, script, pageUrl);
        }

        foreach (var module in moduleEntries)
        {
            RunModule(engine, module, pageUrl);
        }
        RenderProfiler.Stop("phase.bundleExec", bundleExecutionTime);

        engine.CallGlobal("__crawlerFireDomContentLoaded");

        var drainTime = RenderProfiler.Start();
        await DrainJsAsync(engine, documentBaseUri, pageUri, client, pageUrl, cancellationToken);
        RenderProfiler.Stop("phase.drain", drainTime);

        if (_options.EnableStreams)
        {
            var restored = engine.Evaluate<int>("__crawlerGuardRegression()");
            if (restored >= 0)
                _logger.LogDebug("JS render regressed below the server-rendered shell on '{url}'; restored baseline ({anchors} anchors).", pageUrl, restored);
        }

        return Finalize(engine, finalize, totalTime);
    }

    private static T Finalize<T>(IJsEngine engine, Func<IJsEngine, T> finalize, long totalTime)
    {
        var finalizeTime = RenderProfiler.Start();
        var result = finalize(engine);
        RenderProfiler.Stop("phase.finalize", finalizeTime);
        RenderProfiler.Stop("phase.total", totalTime);

        if (DomProfiler.Enabled)
            DomProfiler.Add(engine.Evaluate<string>("__crawlerDomProfileDump()"));

        return result;
    }

    // Relative script/resource URLs resolve against the document base URL (the first <base href>, if any),
    // not the page URL — matching the browser. Without this, a <base href="/"> page served from a nested
    // path fetches the site's HTML fallback for every relative <script src>, and the engine aborts on it.
    private static Uri ResolveDocumentBase(IJsEngine engine, Uri pageUri)
    {
        var baseHref = engine.Evaluate<string>("__crawlerGetBaseHref()");
        if (!string.IsNullOrEmpty(baseHref) && Uri.TryCreate(pageUri, baseHref, out var baseUri))
            return baseUri;

        return pageUri;
    }

    private static async Task<(IReadOnlyList<RegularScript> Regular, IReadOnlyList<ModuleScript> Modules)> CollectScriptsFromJsAsync(IJsEngine engine, Uri baseUri, string pageUrl, HttpClient client, SourceCache sources, CancellationToken cancellationToken)
    {
        var regular = new List<RegularScript>();
        var modules = new List<ModuleScript>();

        var json = engine.Evaluate<string>("__crawlerCollectScripts()");
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
                    regular.Add(new RegularScript(source, absolute.ToString(), External: true));
            }
            else
            {
                if (string.IsNullOrEmpty(text))
                    continue;

                if (isModule)
                    modules.Add(new ModuleScript(pageUrl, text, External: false));
                else
                    regular.Add(new RegularScript(text, pageUrl, External: false));
            }
        }

        return (regular, modules);
    }

    private void RunRegularJs(IJsEngine engine, RegularScript script, string pageUrl)
    {
        SetCurrentScript(engine, script.External ? script.Src : "");
        try
        {
            if (script.External)
                engine.ExecuteCached(script.Src, script.Source);
            else
                engine.Execute(script.Source);
        }
        catch (JsException ex)
        {
            _logger.LogWarning("Bundle execution error on '{url}': {message}\n{details}", pageUrl, ex.Message, ex.ErrorDetails);
        }
        finally
        {
            SetCurrentScript(engine, null);
        }
    }

    // A classic <script> exposes itself as document.currentScript only while it runs synchronously; webpack's
    // auto-public-path (and Next's instanceof-HTMLScriptElement invariant over it) reads that during chunk
    // evaluation, so it must be set around each execution and cleared after — exactly as a browser does.
    private static void SetCurrentScript(IJsEngine engine, string? src)
        => engine.CallGlobal("__crawlerSetCurrentScript", src);

    private async Task DrainJsAsync(IJsEngine engine, Uri baseUri, Uri pageUri, HttpClient client, string pageUrl, CancellationToken cancellationToken)
    {
        // Settle after a few idle turns: __crawlerPending reports the queue depth before __crawlerPump runs
        // it, so a turn with no pending work counts as idle. Runtime-appended <script src>/<link> chunks are
        // loaded at the top of each turn — before the timer pump — so a code-split route's chunk is installed
        // and its load event fired before webpack's chunk-load timeout callback runs.
        var iterations = 0;
        var idle = 0;
        while (iterations++ < _options.MaxTaskDrainIterations && idle < _idleTurnsBeforeSettled)
        {
            var loadedResource = await DrainResourcesAsync(engine, baseUri, pageUri, client, pageUrl, cancellationToken);

            engine.RunMicrotasks();
            var pending = engine.Evaluate<int>("__crawlerPending()");
            engine.Evaluate<int>("__crawlerPump()");
            engine.RunMicrotasks();

            var pendingResources = engine.Evaluate<int>("__crawlerPendingResources()");
            idle = pending == 0 && pendingResources == 0 && !loadedResource ? idle + 1 : 0;
        }
    }

    private async Task<bool> DrainResourcesAsync(IJsEngine engine, Uri baseUri, Uri pageUri, HttpClient client, string pageUrl, CancellationToken cancellationToken)
    {
        var json = engine.Evaluate<string>("__crawlerTakeResources()");
        if (string.IsNullOrEmpty(json))
            return false;

        using var doc = JsonDocument.Parse(json);
        var loaded = false;
        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            var id = entry.GetProperty("id").GetInt32();
            var tag = entry.TryGetProperty("tag", out var tagProp) ? tagProp.GetString() : null;
            var src = entry.TryGetProperty("src", out var srcProp) ? srcProp.GetString() : null;

            await LoadResourceAsync(engine, id, tag, src, baseUri, pageUri, client, pageUrl, cancellationToken);
            loaded = true;
        }

        return loaded;
    }

    // A same-origin <script> is fetched and executed so a webpack chunk's module registrations run; a <link>
    // is treated as loaded without fetching (a crawl needs no CSS). Cross-origin scripts (AppInsights/GTM and
    // similar analytics SDKs) are left pending — running them is slow and yields no links, and nothing awaits
    // their load. Every other case fires the node's load (or error) event to settle the awaiting import().
    private async Task LoadResourceAsync(IJsEngine engine, int id, string? tag, string? src, Uri baseUri, Uri pageUri, HttpClient client, string pageUrl, CancellationToken cancellationToken)
    {
        if (!string.Equals(tag, "script", StringComparison.Ordinal))
        {
            FireResourceEvent(engine, id, "load");
            return;
        }

        if (string.IsNullOrEmpty(src) || !Uri.TryCreate(baseUri, src, out var absolute))
        {
            FireResourceEvent(engine, id, "error");
            return;
        }

        if (!string.Equals(absolute.Host, pageUri.Host, StringComparison.OrdinalIgnoreCase))
            return;

        var source = await FetchSourceAsync(client, _sources, absolute, cancellationToken);
        if (source is null)
        {
            FireResourceEvent(engine, id, "error");
            return;
        }

        SetCurrentScript(engine, absolute.AbsoluteUri);
        try
        {
            engine.ExecuteCached(absolute.AbsoluteUri, source);
        }
        catch (JsException ex)
        {
            _logger.LogWarning("Chunk execution error on '{url}': {message}\n{details}", pageUrl, ex.Message, ex.ErrorDetails);
            FireResourceEvent(engine, id, "error");
            return;
        }
        finally
        {
            SetCurrentScript(engine, null);
        }

        FireResourceEvent(engine, id, "load");
    }

    private static void FireResourceEvent(IJsEngine engine, int id, string type)
        => engine.CallGlobal("__crawlerFireResourceEvent", id, type);

    private static JsExtract CollectLinks(IJsEngine engine)
    {
        var json = engine.Evaluate<string>("__crawlerCollectLinks()");
        if (string.IsNullOrEmpty(json))
            return new JsExtract(null, null, []);

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

        return new JsExtract(canonical, robots, hrefs);
    }

    private static byte[] SerializeJs(IJsEngine engine)
    {
        var html = engine.Evaluate<string>("__crawlerSerialize()");
        return string.IsNullOrEmpty(html) ? [] : _utf8NoBom.GetBytes(html);
    }

    private static void RunPrelude(IJsEngine engine, in PreludeEntry prelude) => engine.ExecuteCached(prelude.Key, prelude.Source);

    // The bundle's console.* calls reach the logger only when ScriptLogging opts in: the JS console stays a
    // no-op until __crawlerSetLogLevel raises it off Infinity, so unset means no embedding and no formatting
    // cost. The level numbers match LogLevel's, so the floor round-trips and __crawlerLog casts straight back.
    private void ConfigureScriptLogging(IJsEngine engine)
    {
        if (_options.ScriptLogging is not { } level)
            return;

        engine.EmbedFunction("__crawlerLog", LogFromScript);
        engine.CallGlobal("__crawlerSetLogLevel", (int)level);
    }

    // Embedded unconditionally (unlike the opt-in console bridge): the task pump and resource-event loop
    // deliberately swallow exceptions from scheduled callbacks so one bad chunk can't abort the drain, which
    // is exactly why a fatal hydration/commit throw otherwise presents as "the render just settled" with no
    // diagnostics. Routing those catches through this channel turns them into a named exception with a stack
    // the moment the renderer's log level is Debug, without spamming a normal crawl by default.
    private void ConfigureDiagnostics(IJsEngine engine)
        => engine.EmbedFunction("__crawlerDiagnostic", ReportDiagnostic);

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

    private void RunModule(IJsEngine engine, ModuleScript module, string pageUrl)
    {
        try
        {
            engine.EvaluateModule(module.Specifier, module.Source, module.External);
        }
        catch (JsException ex)
        {
            _logger.LogWarning("Module execution error on '{url}': {message}\n{details}", pageUrl, ex.Message, ex.ErrorDetails);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Importing a module runs the engine's loader — host code (fetch, parse) that can throw raw CLR
            // exceptions the engine never surfaces as a JsException. A single failed module must not abort the
            // whole page render, so anything short of cancellation is logged and the page continues.
            _logger.LogWarning("Module load error on '{url}': {message}", pageUrl, ex.Message);
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

    private static async Task<string?> FetchSourceAsync(HttpClient client, SourceCache sources, Uri absolute, CancellationToken cancellationToken)
    {
        if (sources.TryGet(absolute, out var cached))
            return cached;

        using var response = await client.GetAsync(absolute, cancellationToken);
        var source = response.IsSuccessStatus() ? await response.Content.ReadAsStringAsync(cancellationToken) : null;
        return sources.Store(absolute, source);
    }
}
