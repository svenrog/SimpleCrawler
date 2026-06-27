using Crawler.AngleSharp.Js.Abstractions;

namespace Crawler.AngleSharp.Js.Jint;

internal sealed class JintJsEngineFactory : IJsEngineFactory
{
    private readonly JintModuleCache _moduleCache = new();
    private readonly JintScriptCache _scriptCache = new();

    public IJsEngine Create(IModuleFetcher fetcher, Uri baseUri)
    {
        return new JintJsEngine(fetcher, baseUri, _moduleCache, _scriptCache);
    }
}
