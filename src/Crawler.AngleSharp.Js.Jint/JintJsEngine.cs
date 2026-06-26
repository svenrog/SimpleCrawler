using Crawler.AngleSharp.Js.Abstractions;
using Crawler.AngleSharp.Js.Errors;
using Crawler.AngleSharp.Js.Models;
using Jint;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;
using System.Globalization;

namespace Crawler.AngleSharp.Js.Jint;

internal sealed class JintJsEngine : IJsEngine
{
    private readonly Engine _engine;

    public JintJsEngine(IModuleFetcher fetcher, Uri baseUri)
    {
        _engine = new Engine(options => options.EnableModules(new JintModuleLoader(fetcher, baseUri)));
    }

    public void EmbedHostObject(string name, object value)
    {
        _engine.SetValue(name, value);
    }

    public void EmbedHostType(string name, Type type)
    {
        _engine.SetValue(name, TypeReference.CreateTypeReference(_engine, type));
    }

    public void EmbedFunction(string name, VFunc function)
    {
        // Jint binds JS calls to a fixed-arity delegate leniently (missing args become null, extra
        // args are ignored), so a four-parameter adapter covers every global the bundle calls.
        _engine.SetValue(name, (Func<object?, object?, object?, object?, object?>)((a, b, c, d) => function(a, b, c, d)));
    }

    public void Execute(string script)
    {
        try
        {
            _engine.Execute(script);
        }
        catch (JavaScriptException ex)
        {
            throw new JsScriptException(ex.Message, ex);
        }
    }

    public void EvaluateModule(string specifier, string source)
    {
        try
        {
            _engine.Modules.Add(specifier, source);
            _engine.Modules.Import(specifier);
        }
        catch (JavaScriptException ex)
        {
            throw new JsScriptException(ex.Message, ex);
        }
    }

    public T Evaluate<T>(string expression)
    {
        var value = _engine.Evaluate(expression).ToObject();
        if (value is T typed)
            return typed;

        return (T)Convert.ChangeType(value!, typeof(T), CultureInfo.InvariantCulture);
    }

    public object CreateArray(IReadOnlyList<object?> items)
    {
        return JsValue.FromObject(_engine, items.ToArray());
    }

    public void InvokeCallback(object callback)
    {
        if (callback is Func<JsValue, JsValue[], JsValue> function)
            function(JsValue.Undefined, []);
        else if (callback is JsValue value)
            _engine.Invoke(value);
    }

    public void RunMicrotasks()
    {
        _engine.Evaluate("0");
    }

    public void Dispose()
    {
        (_engine as IDisposable)?.Dispose();
    }
}
