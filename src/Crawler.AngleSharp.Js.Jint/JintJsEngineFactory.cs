using Crawler.AngleSharp.Js.Abstractions;

namespace Crawler.AngleSharp.Js.Jint;

internal sealed class JintJsEngineFactory : IJsEngineFactory
{
    public IJsEngine Create(IModuleFetcher fetcher, Uri baseUri)
    {
        return new JintJsEngine(fetcher, baseUri);
    }
}
