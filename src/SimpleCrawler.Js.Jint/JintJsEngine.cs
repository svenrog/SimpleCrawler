using Jint;
using Jint.Runtime;
using SimpleCrawler.Js.Abstractions;
using SimpleCrawler.Js.Errors;
using SimpleCrawler.Js.Models;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.ExceptionServices;

namespace SimpleCrawler.Js.Jint;

internal sealed class JintJsEngine : IJsEngine, IDisposable
{
    /// <summary>
    /// How deep one function may recur before the engine stops it. Jint interprets JS on the CLR stack, so
    /// runaway recursion in page code is a <see cref="StackOverflowException"/> — which .NET does not let a
    /// host catch: the process dies, taking the whole crawl with it, where a browser answers the same code
    /// with a catchable "maximum call stack size exceeded". Measured ceiling on the default 1&#160;MB stack is
    /// ~750 nested calls, and frame weight barely moves it (a closure-and-try/catch frame reached the same
    /// depth as a bare one), so this sits below it with room and far above what page code legitimately nests.
    /// <para>
    /// Jint counts occurrences of <em>one</em> function rather than total stack depth, so this catches self-
    /// and mutual recursion — what runaway page code is — and not a deep walk that never repeats a function.
    /// </para>
    /// <c>// declined: LimitMemory</c> — it measures <em>cumulative</em> allocation on the thread that started
    /// the script, not live heap, so a long render trips it on garbage it has already collected, and it
    /// silently stops checking once an async continuation resumes elsewhere. A limit that both false-positives
    /// and quietly stops applying is worse than none.
    /// </summary>
    private const int _maxRecursionDepth = 500;

    /// <summary>
    /// The stack every piece of this engine's JavaScript runs on. A thread-pool thread gets ~1&#160;MB, which
    /// interpreted JS exhausts at a few hundred nested calls — measured against live pages, ~270 was enough,
    /// because each JS call costs several CLR frames and nested expressions inside it cost more. Reserved
    /// address space, committed as used, so the size is not what it costs to have.
    /// <para>
    /// The recursion limit alone does not cover this: Jint counts one function's own repetitions, so
    /// recursion that cycles through several functions reaches the stack's end without any counter passing
    /// its limit. The two are complements — the limit stops a runaway function early and catchably, the
    /// stack decides what "deep but legitimate" a page is allowed to be.
    /// </para>
    /// </summary>
    private const int _stackSize = 16 * 1024 * 1024;

    private readonly BlockingCollection<Action> _work = new(new ConcurrentQueue<Action>());
    private readonly Thread _worker;
    private readonly int _workerThreadId;
    private readonly Engine _engine;
    private readonly JintModuleCache _moduleCache;
    private readonly JintScriptCache _scriptCache;

    public JintJsEngine(JintModuleCache moduleCache, JintScriptCache scriptCache, IModuleFetcher fetcher, Uri baseUri)
    {
        _moduleCache = moduleCache;
        _scriptCache = scriptCache;

        // Every engine call is marshalled here rather than run on the caller's thread, because the caller is
        // an async pipeline: its continuations land on whichever thread-pool thread resumes them, so a stack
        // sized at the top would not be the stack the JS after the first await runs on.
        _worker = new Thread(Work, _stackSize) { IsBackground = true, Name = "simplecrawler-js" };
        _worker.Start();
        _workerThreadId = _worker.ManagedThreadId;

        var loader = new JintModuleLoader(moduleCache, fetcher, baseUri);
        _engine = new Engine(options =>
        {
            // Convert exceptions thrown by host objects (e.g. `new URL('not-a-url')`, which a bundle wraps in
            // a try/catch to probe validity) into catchable JS errors. Jint otherwise bubbles them straight to
            // the CLR host, escaping the bundle's try/catch and aborting the whole render — ClearScript/V8
            // already surface host exceptions as JS errors, so this matches that behaviour.
            options
                .EnableModules(loader)
                .CatchClrExceptions()
                .LimitRecursion(_maxRecursionDepth);

            // Preserves correctness after 4.11 change https://github.com/sebastienros/jint/issues/2560
            // https://github.com/sebastienros/jint/pull/2562
            options.RetainFunctionSourceText = true;
        });
    }

    /// <summary>Runs queued engine work until <see cref="Dispose"/> closes the queue.</summary>
    private void Work()
    {
        foreach (var job in _work.GetConsumingEnumerable())
        {
            job();
        }
    }

    /// <summary>
    /// Runs <paramref name="job"/> on the engine's own thread and waits for it, rethrowing what it threw with
    /// the original stack. A job that re-enters — a host function the running script called, calling back in —
    /// runs inline: it is already on that thread, and queueing it would wait for a queue only it can drain.
    /// </summary>
    private T Run<T>(Func<T> job)
    {
        if (Environment.CurrentManagedThreadId == _workerThreadId)
        {
            return job();
        }

        using var completed = new ManualResetEventSlim(false);
        var result = default(T)!;
        ExceptionDispatchInfo? failure = null;

        _work.Add(() =>
        {
            try
            {
                result = job();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                completed.Set();
            }
        });

        completed.Wait();
        failure?.Throw();
        return result;
    }

    private void Run(Action job) => Run<object?>(() =>
    {
        job();
        return null;
    });

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
        Run(() => _engine.SetValue(name, value));
    }

    public void EmbedFunction(string name, VFunc function)
    {
        // Jint binds JS calls to a fixed-arity delegate leniently (missing args become null, extra
        // args are ignored), so a four-parameter adapter covers every global the bundle calls.
        Run(() => _engine.SetValue(
            name, (Func<object?, object?, object?, object?, object?>)((a, b, c, d) => function(a, b, c, d))));
    }

    public void Execute(string script)
    {
        try
        {
            Run(() => _engine.Execute(script));
        }
        catch (JavaScriptException ex)
        {
            throw new JsException(ex.Message, ex.JavaScriptStackTrace, ex);
        }
        catch (RecursionDepthOverflowException ex)
        {
            throw Recursed(ex);
        }
    }

    public void ExecuteCached(string cacheKey, string script)
    {
        try
        {
            var prepared = _scriptCache.GetOrPrepare(cacheKey, script);
            Run(() => _engine.Execute(in prepared));
        }
        catch (JavaScriptException ex)
        {
            throw new JsException(ex.Message, ex.JavaScriptStackTrace, ex);
        }
        catch (RecursionDepthOverflowException ex)
        {
            throw Recursed(ex);
        }
    }

    /// <summary>
    /// Presents the recursion limit as the script failure it is. It arrives as a CLR exception rather than a
    /// JS one — a bundle's own <c>try</c> cannot see it — so without this it escapes the caller's per-script
    /// isolation and costs the whole render what one runaway script should have cost.
    /// </summary>
    private static JsException Recursed(RecursionDepthOverflowException ex) =>
        new($"Recursion limit of {_maxRecursionDepth} exceeded: {ex.Message}", ex.CallChain, ex);

    public void EvaluateModule(string specifier, string source, bool cache)
    {
        try
        {
            // An inline module's specifier is the page URL — unique per page, so caching its parsed form
            // would retain one AST per crawled page; only stable-URL modules go through the shared cache.
            var prepared = cache ? _moduleCache.GetOrPrepare(specifier, source) : Engine.PrepareModule(source, specifier);
            Run(() =>
            {
                _engine.Modules.Add(specifier, builder => builder.AddModule(in prepared));
                _engine.Modules.Import(specifier);
            });
        }
        catch (JavaScriptException ex)
        {
            throw new JsException(ex.Message, ex.JavaScriptStackTrace, ex);
        }
        catch (RecursionDepthOverflowException ex)
        {
            throw Recursed(ex);
        }
    }

    public T Evaluate<T>(string expression)
    {
        var value = Run(() => _engine.Evaluate(expression).ToObject());
        if (value is T typed)
            return typed;

        return (T)Convert.ChangeType(value!, typeof(T), CultureInfo.InvariantCulture);
    }

    public void CallGlobal(string name, params object?[] args)
    {
        Run(() => _engine.Invoke(name, args!));
    }

    public void RunMicrotasks()
    {
        Run(() => _engine.Evaluate("0"));
    }

    public void Dispose()
    {
        Run(_engine.Dispose);
        _work.CompleteAdding();
        _worker.Join();
        _work.Dispose();
    }
}
