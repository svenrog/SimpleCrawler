using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Js.Abstractions;
using SimpleCrawler.Js.Errors;
using SimpleCrawler.Js.Models;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using System.Globalization;

namespace SimpleCrawler.Js.V8;

internal sealed class V8JsEngine : IJsEngine, IDisposable
{
    private const int _moduleEvaluationTimeoutMs = 30000;
    private const int _stackTraceContextRadius = 48;

    /// <summary>
    /// AddPerformanceObject exposes a native high-resolution `Performance.now()` global (capital P; our
    /// lowercase `performance` shim stays ours) and SetTimerResolution sharpens it to ~100ns — enough to
    /// time individual DOM ops. Both are added only under JSRENDER_DOM_PROFILE so a normal crawl neither
    /// pays SetTimerResolution's process-wide timer bump nor exposes the extra global.
    /// </summary>
    private static readonly V8ScriptEngineFlags _engineFlags = BuildEngineFlags();

    private static V8ScriptEngineFlags BuildEngineFlags()
    {
        var flags = V8ScriptEngineFlags.EnableDynamicModuleImports | V8ScriptEngineFlags.EnableTaskPromiseConversion;

        if (Environment.GetEnvironmentVariable("JSRENDER_DOM_PROFILE") is "1" or "true")
            flags |= V8ScriptEngineFlags.AddPerformanceObject | V8ScriptEngineFlags.SetTimerResolution;

        return flags;
    }

    private readonly V8RuntimePool _pool;
    private readonly V8RuntimeLease _lease;
    private readonly V8ScriptEngine _engine;
    private readonly V8ModuleLoader _loader;
    private readonly V8StackTraceFormatter _stackTraceFormatter = new(_stackTraceContextRadius);

    public V8JsEngine(IModuleFetcher fetcher, Uri baseUri, V8RuntimePool pool)
    {
        // A fresh context on a pooled isolate, not a fresh isolate: the context's globals are isolated
        // (so per-page state is clean) while the isolate's heap and compilation cache carry over.
        // EnableDynamicModuleImports: V8 rejects import() as "Not supported" otherwise (SPAs use it
        // for lazy routes). EnableTaskPromiseConversion: lets us await the entry's import() as a Task.
        _pool = pool;
        _lease = pool.Rent();
        _engine = _lease.Runtime.CreateScriptEngine(_engineFlags);
        _loader = new V8ModuleLoader(fetcher, baseUri);
        // EnableWebLoading only, never EnableAllLoading: every import is served by our own V8ModuleLoader,
        // whose fetcher refuses any scheme but http/https, so file loading is never legitimately needed.
        // Withholding EnableFileLoading removes a latent local-file-read primitive — untrusted page JS can
        // resolve an import() to a file:// URI, and this keeps that path from ever reaching the disk.
        _engine.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableWebLoading;
        _engine.DocumentSettings.Loader = _loader;
    }

    /// <summary>
    /// Every V8 page runs on a fresh context (isolated globals) even though the isolate is pooled, so the DOM
    /// prelude is always installed anew; there is no realm to reset.
    /// </summary>
    public bool BeginPage() => true;

    public void EmbedHostObject(string name, object value)
    {
        _engine.AddHostObject(name, value);
    }

    public void EmbedFunction(string name, VFunc function)
    {
        // V8 maps a JS call's arguments straight onto a params-array delegate, so one variadic
        // delegate handles every arity the bundle calls these globals with.
        _engine.AddHostObject(name, function);
    }

    public void Execute(string script) => ExecuteNamed("inline", script);

    private void ExecuteNamed(string documentName, string script)
    {
        try
        {
            _engine.Execute(new DocumentInfo(documentName), script);
        }
        catch (ScriptEngineException ex)
        {
            throw new JsException(ex.Message, _stackTraceFormatter.Format(ex.Message, ex.ErrorDetails), ex);
        }
    }

    /// <summary>
    /// V8 runs each page on a pooled isolate whose compilation cache already survives across the per-page
    /// contexts, so re-running the same source is cheap to reparse; the cache key (meaningful only to
    /// Jint's cross-engine Prepared&lt;Script&gt;) is ignored and the source executes directly. The cache key is
    /// the chunk URL, so it doubles as the stack-frame document name (a bare Execute shows only "Script [N]").
    /// </summary>
    public void ExecuteCached(string cacheKey, string script) => ExecuteNamed(cacheKey, script);

    /// <summary>
    /// Loading the entry via Execute creates a separate instance from the one the loader serves
    /// when chunks circularly import it, duplicating module singletons. Seeding the cached loader
    /// and importing keeps a single canonical instance; await drives V8's module evaluation.
    /// cache is ignored: V8 keeps a per-engine module loader cache that is released with the context, so
    /// there is no cross-page accumulation to gate (unlike Jint's shared, crawl-lived module cache).
    /// </summary>
    public void EvaluateModule(string specifier, string source, bool cache)
    {
        try
        {
            var uri = new Uri(specifier);
            _loader.Seed(uri, source);

            var promise = _engine.Evaluate($"import({JsonLiteral.String(specifier)})");
            var task = (Task<object>)JavaScriptExtensions.ToTask(promise);
            task.Wait(_moduleEvaluationTimeoutMs);
        }
        catch (ScriptEngineException ex)
        {
            throw new JsException(ex.Message, _stackTraceFormatter.Format(ex.Message, ex.ErrorDetails), ex);
        }
        catch (AggregateException ex) when (ex.InnerException is ScriptEngineException inner)
        {
            throw new JsException(inner.Message, _stackTraceFormatter.Format(inner.Message, inner.ErrorDetails), inner);
        }
    }

    public T Evaluate<T>(string expression)
    {
        var value = _engine.Evaluate(expression);
        if (value is T typed)
            return typed;

        return (T)Convert.ChangeType(value!, typeof(T), CultureInfo.InvariantCulture);
    }

    public void CallGlobal(string name, params object?[] args)
    {
        _engine.Invoke(name, args!);
    }

    public void RunMicrotasks()
    {
        _engine.Evaluate("0");
    }

    public void Dispose()
    {
        _engine.Dispose();
        _pool.Return(_lease);
    }
}
