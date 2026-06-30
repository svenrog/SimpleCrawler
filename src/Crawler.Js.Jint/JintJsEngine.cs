using Crawler.Js.Abstractions;
using Crawler.Js.Errors;
using Crawler.Js.Models;
using Jint;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;
using System.Globalization;

namespace Crawler.Js.Jint;

internal sealed class JintJsEngine : IJsEngine
{
    private readonly Engine _engine;
    private readonly JintModuleCache _moduleCache;
    private readonly JintScriptCache _scriptCache;

    public JintJsEngine(IModuleFetcher fetcher, Uri baseUri, JintModuleCache moduleCache, JintScriptCache scriptCache)
    {
        _moduleCache = moduleCache;
        _scriptCache = scriptCache;

        _engine = new Engine(options =>
        {
            // Convert exceptions thrown by host objects (e.g. `new URL('not-a-url')`, which a bundle wraps in
            // a try/catch to probe validity) into catchable JS errors. Jint otherwise bubbles them straight to
            // the CLR host, escaping the bundle's try/catch and aborting the whole render — ClearScript/V8
            // already surface host exceptions as JS errors, so this matches that behaviour.
            options
                .EnableModules(new JintModuleLoader(fetcher, baseUri, moduleCache))
                .CatchClrExceptions();
        });
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

    public void ExecuteCached(string cacheKey, string script)
    {
        try
        {
            var prepared = _scriptCache.GetOrPrepare(cacheKey, script);
            _engine.Execute(in prepared);
        }
        catch (JavaScriptException ex)
        {
            throw new JsException(ex.Message, ex.JavaScriptStackTrace, ex);
        }
    }

    public void EvaluateModule(string specifier, string source, bool cache)
    {
        try
        {
            // An inline module's specifier is the page URL — unique per page, so caching its parsed form
            // would retain one AST per crawled page; only stable-URL modules go through the shared cache.
            var prepared = cache ? _moduleCache.GetOrPrepare(specifier, source) : Engine.PrepareModule(source, specifier);
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

    // The native global object reference (not ToObject()'d), so a host getter like document.defaultView
    // can hand the bundle back the same `window` it already reads through globalThis.
    public object GetGlobalObject() => _engine.Evaluate("globalThis");

    public object CreateArray(IReadOnlyList<object?> items)
    {
        var array = items as object?[] ?? [.. items];
        return JsValue.FromObject(_engine, array);
    }

    public void InvokeCallback(object callback)
    {
        // Invoke through the engine, not the marshalled Func delegate directly: the delegate path runs the
        // body without an active evaluation context, so a callee's default-parameter eval throws a bare NRE.
        var function = (callback as Delegate)?.Target as JsValue ?? callback as JsValue;
        try
        {
            if (function is not null)
                _engine.Invoke(function);
            else if (callback is Func<JsValue, JsValue[], JsValue> raw)
                raw(JsValue.Undefined, []);
        }
        catch (JavaScriptException ex)
        {
            throw new JsException(ex.Message, ex.JavaScriptStackTrace, ex);
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
        (_engine as IDisposable)?.Dispose();
    }
}
