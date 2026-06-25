using Jint;
using Jint.Runtime;
using System.Globalization;

namespace Crawler.AngleSharp.Js.Jint;

internal sealed class JintSpaEngine : ISpaEngine
{
    private readonly Engine _engine;

    public JintSpaEngine(IModuleFetcher fetcher, Uri baseUri)
    {
        _engine = new Engine(options => options.EnableModules(new JintModuleLoader(fetcher, baseUri)));
    }

    public void EmbedHostObject(string name, object value)
    {
        _engine.SetValue(name, value);
    }

    public void Execute(string script)
    {
        try
        {
            _engine.Execute(script);
        }
        catch (JavaScriptException ex)
        {
            throw new SpaScriptException(ex.Message, ex);
        }
    }

    public void EvaluateModule(string specifier, string source)
    {
        try
        {
            _engine.Modules.Add(specifier, source);
            _engine.Modules.Import(specifier);
        }
        catch (JavaScriptException ex)
        {
            throw new SpaScriptException(ex.Message, ex);
        }
    }

    public T Evaluate<T>(string expression)
    {
        var value = _engine.Evaluate(expression).ToObject();
        if (value is T typed)
            return typed;

        return (T)Convert.ChangeType(value!, typeof(T), CultureInfo.InvariantCulture);
    }

    public void Dispose()
    {
        (_engine as IDisposable)?.Dispose();
    }
}
