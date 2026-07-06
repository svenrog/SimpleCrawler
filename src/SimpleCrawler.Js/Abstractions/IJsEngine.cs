using Crawler.Js.Models;

namespace Crawler.Js.Abstractions;

public interface IJsEngine : IDisposable
{
    // Begins a page on this engine. Returns true when the caller must install the DOM prelude (a fresh realm —
    // always the case for V8, and the first use of a pooled Jint engine); returns false when the engine reused
    // an existing realm and has already reset its per-page state, so the ~90KB dom.js re-eval can be skipped.
    bool BeginPage();

    void EmbedHostObject(string name, object value);

    void EmbedHostType(string name, Type type);

    void EmbedFunction(string name, VFunc function);

    void Execute(string script);

    void ExecuteCached(string cacheKey, string script);

    void EvaluateModule(string specifier, string source, bool cache);

    T Evaluate<T>(string expression);

    object GetGlobalObject();

    object CreateArray(IReadOnlyList<object?> items);

    void InvokeCallback(object callback);

    void CallGlobal(string name, params object?[] args);

    void RunMicrotasks();
}
