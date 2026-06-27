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
    private readonly JintModuleCache _moduleCache;
    private JsValue? _scriptElementFactory;

    public JintJsEngine(IModuleFetcher fetcher, Uri baseUri, JintModuleCache moduleCache)
    {
        _moduleCache = moduleCache;
        _engine = new Engine(options => options.EnableModules(new JintModuleLoader(fetcher, baseUri, moduleCache)));
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
            throw new JsException(ex.Message, ex.JavaScriptStackTrace, ex);
        }
    }

    public void EvaluateModule(string specifier, string source)
    {
        try
        {
            var prepared = _moduleCache.GetOrPrepare(specifier, source);
            _engine.Modules.Add(specifier, builder => builder.AddModule(in prepared));
            _engine.Modules.Import(specifier);
        }
        catch (JavaScriptException ex)
        {
            throw new JsException(ex.Message, ex.JavaScriptStackTrace, ex);
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
        var array = items as object?[] ?? [.. items];
        return JsValue.FromObject(_engine, array);
    }

    // Next's auto-public-path asserts document.currentScript is `instanceof HTMLScriptElement` and reads
    // its src; a host wrapper can't satisfy instanceof, so hand back a real JS instance of the (JS-defined)
    // HTMLScriptElement class. Returns the native JsValue so the prototype chain survives the round-trip.
    public object CreateScriptElement(string src)
    {
        _scriptElementFactory ??= _engine.Evaluate(
            "(function(u){var s=new HTMLScriptElement();s.src=u;s.getAttribute=function(n){return n==='src'?u:null;};return s;})");
        return _engine.Invoke(_scriptElementFactory, src);
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
