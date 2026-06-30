using Crawler.Js.Abstractions;
using Microsoft.Extensions.Options;

namespace Crawler.Js.V8;

internal sealed class V8JsEngineFactory : IJsEngineFactory, IDisposable
{
    private readonly V8RuntimePool _pool;

    public V8JsEngineFactory(IOptions<V8EngineOptions> options)
    {
        _pool = new V8RuntimePool(options.Value.MaxHeapSizeMb);
    }

    public IJsEngine Create(IModuleFetcher fetcher, Uri baseUri)
    {
        return new V8JsEngine(fetcher, baseUri, _pool);
    }

    public void Dispose()
    {
        _pool.Dispose();
    }
}
