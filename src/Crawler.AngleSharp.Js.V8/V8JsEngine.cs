using Crawler.AngleSharp.Js.Abstractions;
using Crawler.AngleSharp.Js.Errors;
using Crawler.AngleSharp.Js.Models;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using System.Globalization;
using System.Text.Json;

namespace Crawler.AngleSharp.Js.V8;

internal sealed class V8JsEngine : IJsEngine
{
    private const int _moduleEvaluationTimeoutMs = 30000;
    private const int _stackTraceContextRadius = 48;

    private readonly V8RuntimePool _pool;
    private readonly V8Runtime _runtime;
    private readonly V8ScriptEngine _engine;
    private readonly V8ModuleLoader _loader;
    private readonly V8StackTraceFormatter _stackTraceFormatter = new(_stackTraceContextRadius);
    private ScriptObject? _arrayFactory;
    private ScriptObject? _scriptElementFactory;

    public V8JsEngine(IModuleFetcher fetcher, Uri baseUri, V8RuntimePool pool)
    {
        // A fresh context on a pooled isolate, not a fresh isolate: the context's globals are isolated
        // (so per-page state is clean) while the isolate's heap and compilation cache carry over.
        // EnableDynamicModuleImports: V8 rejects import() as "Not supported" otherwise (SPAs use it
        // for lazy routes). EnableTaskPromiseConversion: lets us await the entry's import() as a Task.
        _pool = pool;
        _runtime = pool.Rent();
        _engine = _runtime.CreateScriptEngine(V8ScriptEngineFlags.EnableDynamicModuleImports | V8ScriptEngineFlags.EnableTaskPromiseConversion);
        _loader = new V8ModuleLoader(fetcher, baseUri);
        _engine.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableAllLoading;
        _engine.DocumentSettings.Loader = _loader;
    }

    public void EmbedHostObject(string name, object value)
    {
        _engine.AddHostObject(name, value);
    }

    public void EmbedHostType(string name, Type type)
    {
        _engine.AddHostType(name, type);
    }

    public void EmbedFunction(string name, VFunc function)
    {
        // V8 maps a JS call's arguments straight onto a params-array delegate, so one variadic
        // delegate handles every arity the bundle calls these globals with.
        _engine.AddHostObject(name, function);
    }

    public void Execute(string script)
    {
        try
        {
            _engine.Execute(script);
        }
        catch (ScriptEngineException ex)
        {
            throw new JsException(ex.Message, _stackTraceFormatter.Format(ex.Message, ex.ErrorDetails), ex);
        }
    }

    // V8 runs each page on a pooled isolate whose compilation cache already survives across the per-page
    // contexts, so re-running the same source is cheap to reparse; the cache key (meaningful only to
    // Jint's cross-engine Prepared<Script>) is ignored and the source executes directly.
    public void ExecuteCached(string cacheKey, string script) => Execute(script);

    // Loading the entry via Execute creates a separate instance from the one the loader serves
    // when chunks circularly import it, duplicating module singletons. Seeding the cached loader
    // and importing keeps a single canonical instance; await drives V8's module evaluation.
    public void EvaluateModule(string specifier, string source)
    {
        try
        {
            var uri = new Uri(specifier);
            _loader.Seed(uri, source);

            var promise = _engine.Evaluate($"import({JsonSerializer.Serialize(specifier)})");
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

    // The native global object reference (not ToObject()'d), so a host getter like document.defaultView
    // can hand the bundle back the same `window` it already reads through globalThis.
    public object GetGlobalObject() => _engine.Evaluate("globalThis");

    public object CreateArray(IReadOnlyList<object?> items)
    {
        // A plain .NET array reaches JS as a host object without a JS `length`, which breaks
        // Array.prototype.slice.call on it; spreading through a cached JS function yields a real array.
        _arrayFactory ??= (ScriptObject)_engine.Evaluate("(function(){return Array.prototype.slice.call(arguments);})");
        var args = items as object?[] ?? [.. items];
        return _arrayFactory.InvokeAsFunction(args);
    }

    // Next's auto-public-path asserts document.currentScript is `instanceof HTMLScriptElement` and reads
    // its src; a host wrapper can't satisfy instanceof, so hand back a real JS instance of the (JS-defined)
    // HTMLScriptElement class. Returns the native ScriptObject so the prototype chain survives the round-trip.
    public object CreateScriptElement(string src)
    {
        _scriptElementFactory ??= (ScriptObject)_engine.Evaluate(
            "(function(u){var s=new HTMLScriptElement();s.src=u;s.getAttribute=function(n){return n==='src'?u:null;};return s;})");
        return _scriptElementFactory.InvokeAsFunction(src);
    }

    public void InvokeCallback(object callback)
    {
        try
        {
            ((ScriptObject)callback).InvokeAsFunction();
        }
        catch (ScriptEngineException ex)
        {
            throw new JsException(ex.Message, _stackTraceFormatter.Format(ex.Message, ex.ErrorDetails), ex);
        }
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
        _pool.Return(_runtime);
    }
}
