using Crawler.Js.Models;

namespace Crawler.Js.Abstractions;

public interface IJsEngine : IDisposable
{
    void EmbedHostObject(string name, object value);

    void EmbedHostType(string name, Type type);

    void EmbedFunction(string name, VFunc function);

    void Execute(string script);

    void ExecuteCached(string cacheKey, string script);

    void EvaluateModule(string specifier, string source);

    T Evaluate<T>(string expression);

    object GetGlobalObject();

    object CreateArray(IReadOnlyList<object?> items);

    object CreateScriptElement(string src);

    void InvokeCallback(object callback);

    void CallGlobal(string name, params object?[] args);

    void RunMicrotasks();
}
