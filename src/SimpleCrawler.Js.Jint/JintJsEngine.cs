using Jint;
using Jint.Runtime;
using SimpleCrawler.Js.Abstractions;
using SimpleCrawler.Js.Errors;
using SimpleCrawler.Js.Models;
using System.Globalization;

namespace SimpleCrawler.Js.Jint;

internal sealed class JintJsEngine : IJsEngine, IDisposable
{
    /// <summary>
    /// How deep the call stack may go before the engine stops it, at V8's own limit so a page that recurses
    /// meets the same ceiling here as in the browser it was written for. Jint interprets JS on the CLR stack,
    /// so without this runaway recursion in page code is a <see cref="StackOverflowException"/> — which .NET
    /// does not let a host catch: the process dies mid-crawl, taking every page already rendered with it.
    /// <para>
    /// Set, this makes Jint probe the stack it actually has left, continue on a fresh one while the count is
    /// under the limit, and raise a JS <c>RangeError</c> at it — so the page's own <c>try</c> sees what it
    /// would see in a browser. Sizing a thread's stack instead only moves the cliff, and moves it by however
    /// much stack the platform honours.
    /// </para>
    /// <c>// declined: LimitRecursion</c> — it counts one function's own repetitions, and live runaways cycle
    /// through several functions, reaching the stack's end with no counter near its limit. Measured against
    /// the two sites that crashed: it changed nothing.
    /// <c>// declined: LimitMemory</c> — it measures <em>cumulative</em> allocation on the thread that started
    /// the script, not live heap, so a long render trips it on garbage it has already collected, and it
    /// silently stops checking once an async continuation resumes elsewhere.
    /// </summary>
    private const int _maxExecutionStackCount = 2000;

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

            options.Constraints.MaxExecutionStackCount = _maxExecutionStackCount;

            // Preserves correctness after 4.11 change https://github.com/sebastienros/jint/issues/2560
            // https://github.com/sebastienros/jint/pull/2562
            options.RetainFunctionSourceText = true;
        });
    }

    /// <summary>
    /// Each engine renders exactly one page, so the DOM prelude is always installed fresh — returns true. On
    /// current Jint, engine construction and the ~90KB dom.js eval are cheap next to per-page DOM work, so
    /// reusing/resetting a realm across pages isn't worth its complexity (and reflection); measured away.
    /// </summary>
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

    /// <summary>
    /// Runs an external script from its cached parsed form.
    /// </summary>
    /// <remarks>
    /// Preparing happens outside the engine, so a source that does not parse arrives as a CLR
    /// <see cref="ScriptPreparationException"/> rather than as the JS <c>SyntaxError</c> the engine guards an
    /// inline script's parse into. Unwrapped it is not the renderer's per-script failure and escapes the
    /// isolation around this call, costing the page every script after it — and a <c>src</c> answered with an
    /// error page instead of JavaScript is an ordinary thing for a live third-party tag to do.
    /// </remarks>
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
        catch (ScriptPreparationException ex)
        {
            throw new JsException(ex.Message, ex.InnerException?.Message, ex);
        }
    }

    /// <inheritdoc cref="ExecuteCached" />
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
        catch (ScriptPreparationException ex)
        {
            throw new JsException(ex.Message, ex.InnerException?.Message, ex);
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
