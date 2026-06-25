using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using System.Globalization;
using System.Text.Json;

namespace Crawler.AngleSharp.Js.V8;

internal sealed class V8SpaEngine : ISpaEngine
{
    private const int ModuleEvaluationTimeoutMs = 30000;

    private readonly V8ScriptEngine _engine;
    private readonly V8ModuleLoader _loader;

    public V8SpaEngine(IModuleFetcher fetcher, Uri baseUri)
    {
        // EnableDynamicModuleImports: V8 rejects import() as "Not supported" otherwise (SPAs use it
        // for lazy routes). EnableTaskPromiseConversion: lets us await the entry's import() as a Task.
        _engine = new V8ScriptEngine(V8ScriptEngineFlags.EnableDynamicModuleImports | V8ScriptEngineFlags.EnableTaskPromiseConversion);
        _loader = new V8ModuleLoader(fetcher, baseUri);
        _engine.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableAllLoading;
        _engine.DocumentSettings.Loader = _loader;
    }

    public void EmbedHostObject(string name, object value)
    {
        _engine.AddHostObject(name, value);
    }

    public void Execute(string script)
    {
        try
        {
            _engine.Execute(script);
        }
        catch (ScriptEngineException ex)
        {
            throw new SpaScriptException(ex.Message, ex);
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
            task.Wait(ModuleEvaluationTimeoutMs);
        }
        catch (ScriptEngineException ex)
        {
            throw new SpaScriptException(ex.Message, ex);
        }
        catch (AggregateException ex) when (ex.InnerException is ScriptEngineException inner)
        {
            throw new SpaScriptException(inner.Message, inner);
        }
    }

    public T Evaluate<T>(string expression)
    {
        var value = _engine.Evaluate(expression);
        if (value is T typed)
            return typed;

        return (T)Convert.ChangeType(value!, typeof(T), CultureInfo.InvariantCulture);
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}
