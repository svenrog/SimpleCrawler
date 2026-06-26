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

    private readonly V8RuntimePool _pool;
    private readonly V8Runtime _runtime;
    private readonly V8ScriptEngine _engine;
    private readonly V8ModuleLoader _loader;
    private ScriptObject? _arrayFactory;

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
            throw new JsScriptException(ex.Message, ex);
        }
    }

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
            throw new JsScriptException(ex.Message, ex);
        }
        catch (AggregateException ex) when (ex.InnerException is ScriptEngineException inner)
        {
            throw new JsScriptException(inner.Message, inner);
        }
    }

    public T Evaluate<T>(string expression)
    {
        var value = _engine.Evaluate(expression);
        if (value is T typed)
            return typed;

        return (T)Convert.ChangeType(value!, typeof(T), CultureInfo.InvariantCulture);
    }

    public object CreateArray(IReadOnlyList<object?> items)
    {
        // A plain .NET array reaches JS as a host object without a JS `length`, which breaks
        // Array.prototype.slice.call on it; spreading through a cached JS function yields a real array.
        _arrayFactory ??= (ScriptObject)_engine.Evaluate("(function(){return Array.prototype.slice.call(arguments);})");
        var args = items as object?[] ?? [.. items];
        return _arrayFactory.InvokeAsFunction(args);
    }

    public void InvokeCallback(object callback)
    {
        ((ScriptObject)callback).InvokeAsFunction();
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
