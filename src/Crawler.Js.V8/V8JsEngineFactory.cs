using Crawler.Js.Abstractions;

namespace Crawler.Js.V8;

internal sealed class V8JsEngineFactory : IJsEngineFactory, IDisposable
{
    private readonly V8RuntimePool _pool = new();

    public IJsEngine Create(IModuleFetcher fetcher, Uri baseUri)
    {
        return new V8JsEngine(fetcher, baseUri, _pool);
    }

    public void Dispose()
    {
        _pool.Dispose();
    }
}
