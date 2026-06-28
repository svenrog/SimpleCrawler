using Crawler.AngleSharp.Js.Abstractions;
using Crawler.AngleSharp.Js.Dom;
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
    private readonly JintScriptCache _scriptCache;
    private JsValue? _scriptElementFactory;

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

            // A browser's DOM node exposes no own enumerable properties, so Object.keys(el)/for..in/spread/
            // JSON.stringify see nothing and a bundle's deep clone/merge/serialize walker stops at it. Jint's
            // default ObjectWrapper instead reports every CLR getter (children, parentNode, ownerDocument, ...)
            // as an own key, so such a walker follows the DOM's reference cycles forever — overflowing the
            // stack and allocating without bound. Report no enumerable keys for our node wrappers to match the
            // browser; direct member access (el.innerHTML) is unaffected.
            options.Interop.ObjectWrapperReportedPropertyKeys = static (_, target) =>
                target is JsNode ? Array.Empty<JsValue>() : null;
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

    // The native global object reference (not ToObject()'d), so a host getter like document.defaultView
    // can hand the bundle back the same `window` it already reads through globalThis.
    public object GetGlobalObject() => _engine.Evaluate("globalThis");

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
