using SimpleCrawler.Js.Models;

namespace SimpleCrawler.Js.Abstractions;

public interface IJsEngine
{
    /// <summary>
    /// Begins a page on this engine. Returns true when the caller must install the DOM prelude — always the case
    /// today, since each engine renders exactly one page on a fresh realm (V8 a fresh context, Jint a fresh
    /// Engine). The bool is kept so the renderer stays agnostic to how an engine provisions each page's realm.
    /// </summary>
    bool BeginPage();

    void EmbedHostObject(string name, object value);

    void EmbedFunction(string name, VFunc function);

    void Execute(string script);

    void ExecuteCached(string cacheKey, string script);

    void EvaluateModule(string specifier, string source, bool cache);

    T Evaluate<T>(string expression);

    void CallGlobal(string name, params object?[] args);

    void RunMicrotasks();
}
