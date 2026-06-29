using Crawler.AngleSharp.Js.Abstractions;
using Crawler.AngleSharp.Js.Dom.Network;
using Crawler.AngleSharp.Js.Errors;
using Crawler.AngleSharp.Js.Models;
using Crawler.AngleSharp.Js.Services;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Crawler.AngleSharp.Js.Rendering;

public sealed class JsRenderer
{
    private const int _idleTurnsBeforeSettled = 3;

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

        return await RenderJsAsync(shell, pageUrl, client, cancellationToken);
    }

    // The DOM lives entirely in JS (Preludes/dom.js). HTML goes in via __crawlerLoadHtml, the bundle
    // mutates the JS DOM with no managed crossings, timers drain through __crawlerPump, and the tree
    // is serialized back to HTML for the (still AngleSharp-backed, until Phase 7) static extractor.
    private async Task<byte[]> RenderJsAsync(byte[] shell, string pageUrl, HttpClient client, CancellationToken cancellationToken)
    {
        var totalTime = RenderProfiler.Start();

        var pageUri = new Uri(pageUrl);
        var html = _utf8NoBom.GetString(shell);

        var fetcher = new HttpModuleFetcher(client, _sources, cancellationToken);

        IJsEngine rawEngine;
        var createTime = RenderProfiler.Start();
        rawEngine = _engineFactory.Create(fetcher, pageUri);
        RenderProfiler.Stop("phase.engineCreate", createTime);

        using var engine = RenderProfiler.Enabled ? new ProfilingJsEngine(rawEngine) : rawEngine;

        var setupTime = RenderProfiler.Start();
        RunPrelude(engine, JsPreludes.Dom);
        RenderProfiler.Stop("phase.setupGlobals", setupTime);

        engine.CallGlobal("__crawlerSetLocation", pageUrl);

        var parseTime = RenderProfiler.Start();
        try
        {
            engine.CallGlobal("__crawlerLoadHtml", html);
        }
        catch (JsException ex)
        {
            _logger.LogWarning("JS DOM parse error on '{url}': {message}\n{details}", pageUrl, ex.Message, ex.ErrorDetails);
            return shell;
        }
        RenderProfiler.Stop("phase.parse", parseTime);


        IReadOnlyList<RegularScript> regularScripts;
        IReadOnlyList<ModuleScript> moduleEntries;
        var collectTime = RenderProfiler.Start();
        (regularScripts, moduleEntries) = await CollectScriptsFromJsAsync(engine, pageUrl, client, _sources, cancellationToken);
        RenderProfiler.Stop("phase.collect", collectTime);

        byte[] result;
        long serializeTime;

        // The markup had a <script> tag but the parser surfaced nothing executable (e.g. JSON), so the
        // DOM still equals the shell: skip the bundle and just reserialize.
        if (regularScripts.Count == 0 && moduleEntries.Count == 0)
        {
            serializeTime = RenderProfiler.Start();
            result = SerializeJs(engine);
            RenderProfiler.Stop("phase.serialize", serializeTime);
            RenderProfiler.Stop("phase.total", totalTime);
            return result;
        }

        if (_options.EnableFetch)
        {
            engine.EmbedHostObject("__http", new JsHttp(client, pageUri, _logger, cancellationToken));
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

        var drainTime = RenderProfiler.Start();
        DrainJs(engine);
        RenderProfiler.Stop("phase.drain", drainTime);

        serializeTime = RenderProfiler.Start();
        result = SerializeJs(engine);
        RenderProfiler.Stop("phase.serialize", serializeTime);
        RenderProfiler.Stop("phase.total", totalTime);
        return result;
    }

    private static async Task<(IReadOnlyList<RegularScript> Regular, IReadOnlyList<ModuleScript> Modules)> CollectScriptsFromJsAsync(IJsEngine engine, string pageUrl, HttpClient client, SourceCache sources, CancellationToken cancellationToken)
    {
        var baseUri = new Uri(pageUrl);
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
                    modules.Add(new ModuleScript(absolute.ToString(), source));
                else
                    regular.Add(new RegularScript(source, absolute.ToString(), External: true));
            }
            else
            {
                if (string.IsNullOrEmpty(text))
                    continue;

                if (isModule)
                    modules.Add(new ModuleScript(pageUrl, text));
                else
                    regular.Add(new RegularScript(text, pageUrl, External: false));
            }
        }

        return (regular, modules);
    }

    private void RunRegularJs(IJsEngine engine, RegularScript script, string pageUrl)
    {
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
    }

    private void DrainJs(IJsEngine engine)
    {
        // Mirrors the Bridge drain's "settle after idle turns": __crawlerPending reports the queue depth
        // before __crawlerPump runs it, so a turn with no pending work counts as idle.
        var iterations = 0;
        var idle = 0;
        while (iterations++ < _options.MaxTaskDrainIterations && idle < _idleTurnsBeforeSettled)
        {
            engine.RunMicrotasks();
            var pending = engine.Evaluate<int>("__crawlerPending()");
            engine.Evaluate<int>("__crawlerPump()");
            engine.RunMicrotasks();
            idle = pending == 0 ? idle + 1 : 0;
        }
    }

    private byte[] SerializeJs(IJsEngine engine)
    {
        var html = engine.Evaluate<string>("__crawlerSerialize()");
        return string.IsNullOrEmpty(html) ? [] : _utf8NoBom.GetBytes(html);
    }

    private static void RunPrelude(IJsEngine engine, in PreludeEntry prelude) => engine.ExecuteCached(prelude.Key, prelude.Source);

    private void RunModule(IJsEngine engine, ModuleScript module, string pageUrl)
    {
        try
        {
            engine.EvaluateModule(module.Specifier, module.Source);
        }
        catch (JsException ex)
        {
            _logger.LogWarning("Module execution error on '{url}': {message}\n{details}", pageUrl, ex.Message, ex.ErrorDetails);
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
        var source = response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(cancellationToken) : null;
        return sources.Store(absolute, source);
    }
}
