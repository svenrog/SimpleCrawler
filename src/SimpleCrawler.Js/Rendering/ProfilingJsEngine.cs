using Crawler.Js.Abstractions;
using Crawler.Js.Models;

namespace Crawler.Js.Rendering;

// Decorates an engine when JSRENDER_PROFILE is set, attributing time and call counts to each boundary
// crossing so a render profile can split per-page cost between Execute/ExecuteCached, host embeds,
// callbacks, and microtask pumps.
internal sealed class ProfilingJsEngine : IJsEngine
{
    private readonly IJsEngine _inner;

    public ProfilingJsEngine(IJsEngine inner) => _inner = inner;

    public bool BeginPage() => Time("engine.BeginPage", () => _inner.BeginPage());

    public void EmbedHostObject(string name, object value) => Time("engine.EmbedHostObject", () => _inner.EmbedHostObject(name, value));
    public void EmbedHostType(string name, Type type) => Time("engine.EmbedHostType", () => _inner.EmbedHostType(name, type));
    public void EmbedFunction(string name, VFunc function) => Time("engine.EmbedFunction", () => _inner.EmbedFunction(name, function));
    public void Execute(string script) => Time("engine.Execute", () => _inner.Execute(script));
    public void ExecuteCached(string cacheKey, string script) => Time("engine.ExecuteCached", () => _inner.ExecuteCached(cacheKey, script));
    public void EvaluateModule(string specifier, string source, bool cache) => Time("engine.EvaluateModule", () => _inner.EvaluateModule(specifier, source, cache));
    public T Evaluate<T>(string expression) => Time("engine.Evaluate", () => _inner.Evaluate<T>(expression));
    public object GetGlobalObject() => Time("engine.GetGlobalObject", () => _inner.GetGlobalObject());
    public object CreateArray(IReadOnlyList<object?> items) => Time("engine.CreateArray", () => _inner.CreateArray(items));
    public void InvokeCallback(object callback) => Time("engine.InvokeCallback", () => _inner.InvokeCallback(callback));
    public void CallGlobal(string name, params object?[] args) => Time("engine.CallGlobal", () => _inner.CallGlobal(name, args));
    public void RunMicrotasks() => Time("engine.RunMicrotasks", () => _inner.RunMicrotasks());

    public void Dispose() => _inner.Dispose();

    private static void Time(string bucket, Action action)
    {
        var start = RenderProfiler.Start();
        try
        {
            action();
        }
        finally
        {
            RenderProfiler.Stop(bucket, start);
        }
    }

    private static T Time<T>(string bucket, Func<T> func)
    {
        var start = RenderProfiler.Start();
        try
        {
            return func();
        }
        finally
        {
            RenderProfiler.Stop(bucket, start);
        }
    }
}
