using Crawler.AngleSharp.Js.Abstractions;

namespace Crawler.AngleSharp.Js.V8;

internal sealed class V8JsEngineFactory : IJsEngineFactory
{
    public IJsEngine Create(IModuleFetcher fetcher, Uri baseUri)
    {
        return new V8JsEngine(fetcher, baseUri);
    }
}
