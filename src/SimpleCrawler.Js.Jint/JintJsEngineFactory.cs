using SimpleCrawler.Js.Abstractions;

namespace SimpleCrawler.Js.Jint;

internal sealed class JintJsEngineFactory : IJsEngineFactory
{
    private readonly JintModuleCache _moduleCache = new();
    private readonly JintScriptCache _scriptCache = new();

    public IJsEngine Create(IModuleFetcher fetcher, Uri baseUri)
    {
        return new JintJsEngine(_moduleCache, _scriptCache, fetcher, baseUri);
    }
}
