using Crawler.Js.Abstractions;
using Microsoft.Extensions.Options;

namespace Crawler.Js.Jint;

internal sealed class JintJsEngineFactory : IJsEngineFactory, IDisposable
{
    private readonly JintModuleCache _moduleCache = new();
    private readonly JintScriptCache _scriptCache = new();
    private readonly JintEnginePool _pool;

    public JintJsEngineFactory(IOptions<JintEngineOptions> options)
    {
        _pool = new JintEnginePool(options.Value, _moduleCache);
    }

    public IJsEngine Create(IModuleFetcher fetcher, Uri baseUri)
    {
        return new JintJsEngine(_pool, _moduleCache, _scriptCache, fetcher, baseUri);
    }

    public void Dispose()
    {
        _pool.Dispose();
    }
}
