using SimpleCrawler.Js.Abstractions;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.Js.V8;

internal sealed class V8JsEngineFactory : IJsEngineFactory, IDisposable
{
    private readonly V8RuntimePool _pool;
    private readonly V8EngineOptions _options;

    public V8JsEngineFactory(IOptions<V8EngineOptions> options)
    {
        _options = options.Value;
        _pool = new V8RuntimePool(_options);
    }

    public IJsEngine Create(IModuleFetcher fetcher, Uri baseUri, CancellationToken cancellationToken)
    {
        return new V8JsEngine(fetcher, baseUri, _pool, _options, cancellationToken);
    }

    public void Dispose()
    {
        _pool.Dispose();
    }
}
