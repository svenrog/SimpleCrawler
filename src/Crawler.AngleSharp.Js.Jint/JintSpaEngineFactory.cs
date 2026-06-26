using Crawler.AngleSharp.Js.Abstractions;

namespace Crawler.AngleSharp.Js.Jint;

internal sealed class JintSpaEngineFactory : ISpaEngineFactory
{
    public ISpaEngine Create(IModuleFetcher fetcher, Uri baseUri)
    {
        return new JintSpaEngine(fetcher, baseUri);
    }
}
