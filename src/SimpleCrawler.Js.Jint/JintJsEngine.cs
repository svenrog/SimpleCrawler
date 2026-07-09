using Jint;
using Jint.Runtime;
using SimpleCrawler.Js.Abstractions;
using SimpleCrawler.Js.Errors;
using SimpleCrawler.Js.Models;
using System.Globalization;

namespace SimpleCrawler.Js.Jint;

internal sealed class JintJsEngine : IJsEngine, IDisposable
{
    private readonly Engine _engine;
    private readonly JintModuleCache _moduleCache;
    private readonly JintScriptCache _scriptCache;

    public JintJsEngine(JintModuleCache moduleCache, JintScriptCache scriptCache, IModuleFetcher fetcher, Uri baseUri)
    {
        _moduleCache = moduleCache;
        _scriptCache = scriptCache;

        var loader = new JintModuleLoader(moduleCache, fetcher, baseUri);
        _engine = new Engine(options =>
        {
            // Convert exceptions thrown by host objects (e.g. `new URL('not-a-url')`, which a bundle wraps in
            // a try/catch to probe validity) into catchable JS errors. Jint otherwise bubbles them straight to
            // the CLR host, escaping the bundle's try/catch and aborting the whole render — ClearScript/V8
            // already surface host exceptions as JS errors, so this matches that behaviour.
            options
                .EnableModules(loader)
                .CatchClrExceptions();
        });
    }

    // Each engine renders exactly one page, so the DOM prelude is always installed fresh — returns true. On
    // current Jint, engine construction and the ~90KB dom.js eval are cheap next to per-page DOM work, so
    // reusing/resetting a realm across pages isn't worth its complexity (and reflection); measured away.
    public bool BeginPage()
    {
        return true;
    }

    public void EmbedHostObject(string name, object value)
    {
        _engine.SetValue(name, value);
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
    }
}
