using Crawler.Js.Abstractions;
using Crawler.Js.Dom.Network;
using Crawler.Js.Errors;
using Crawler.Js.Models;
using Crawler.Js.Services;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Crawler.Js.Rendering;

public sealed class JsRenderer
{
    private const int _idleTurnsBeforeSettled = 3;

    private static readonly UTF8Encoding _utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly JsExtract _emptyExtract = new(null, null, []);

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

    // Render path (tests/diagnostics inspect the serialized HTML): a scriptless shell can't mutate
    // anything, so it is returned verbatim; otherwise the bundle runs and the tree is serialized.
    public Task<byte[]> RenderAsync(byte[] shell, string pageUrl, HttpClient client, CancellationToken cancellationToken)
    {
        if (!ContainsScriptTag(shell))
            return Task.FromResult(shell);

        return RunAsync(shell, pageUrl, client, cancellationToken, SerializeJs, shell);
    }

    // Crawl path: anchors/canonical/robots are read straight off the live DOM — no serialize, no
    // AngleSharp reparse. Scriptless shells still parse, because extraction needs the tree.
    internal Task<JsExtract> ExtractAsync(byte[] shell, string pageUrl, HttpClient client, CancellationToken cancellationToken)
        => RunAsync(shell, pageUrl, client, cancellationToken, CollectLinks, _emptyExtract);

    // The DOM lives entirely in JS (Preludes/dom.js). HTML goes in via __crawlerLoadHtml, the bundle mutates
    // the JS DOM with no managed crossings, and timers drain through __crawlerPump. `finalize` produces the
    // caller's result from the settled tree (serialize for rendering, collect-links for crawling); `abortValue`
    // is returned if the DOM parse itself fails before the tree exists.
    private async Task<T> RunAsync<T>(byte[] shell, string pageUrl, HttpClient client, CancellationToken cancellationToken, Func<IJsEngine, T> finalize, T abortValue)
    {
        var totalTime = RenderProfiler.Start();

        var pageUri = new Uri(pageUrl);
        var html = _utf8NoBom.GetString(shell);

        var fetcher = new HttpModuleFetcher(client, _sources, cancellationToken);

        var createTime = RenderProfiler.Start();
        var rawEngine = _engineFactory.Create(fetcher, pageUri);
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
            RenderProfiler.Stop("phase.total", totalTime);
            return abortValue;
        }
        RenderProfiler.Stop("phase.parse", parseTime);

        var collectTime = RenderProfiler.Start();
        var (regularScripts, moduleEntries) = await CollectScriptsFromJsAsync(engine, pageUrl, client, _sources, cancellationToken);
        RenderProfiler.Stop("phase.collect", collectTime);

        // The markup had a <script> tag but the parser surfaced nothing executable (e.g. JSON), so nothing
        // ran: finalize against the parsed-only tree.
        if (regularScripts.Count == 0 && moduleEntries.Count == 0)
        {
            return Finalize(engine, finalize, totalTime);
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

        return Finalize(engine, finalize, totalTime);
    }

    private static T Finalize<T>(IJsEngine engine, Func<IJsEngine, T> finalize, long totalTime)
    {
        var finalizeTime = RenderProfiler.Start();
        var result = finalize(engine);
        RenderProfiler.Stop("phase.finalize", finalizeTime);
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
