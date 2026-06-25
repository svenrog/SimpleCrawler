namespace Crawler.AngleSharp.Js;

public interface ISpaEngine : IDisposable
{
    void EmbedHostObject(string name, object value);

    void Execute(string script);

    void EvaluateModule(string specifier, string source);

    T Evaluate<T>(string expression);
}
