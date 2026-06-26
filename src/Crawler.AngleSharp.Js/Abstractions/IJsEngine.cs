using Crawler.AngleSharp.Js.Models;

namespace Crawler.AngleSharp.Js.Abstractions;

public interface IJsEngine : IDisposable
{
    void EmbedHostObject(string name, object value);

    void EmbedHostType(string name, Type type);

    void EmbedFunction(string name, VFunc function);

    void Execute(string script);

    void EvaluateModule(string specifier, string source);

    T Evaluate<T>(string expression);

    object CreateArray(IReadOnlyList<object?> items);

    void InvokeCallback(object callback);

    void RunMicrotasks();
}
