namespace Crawler.AngleSharp.Js.V8;

internal sealed class V8SpaEngineFactory : ISpaEngineFactory
{
    public ISpaEngine Create(IModuleFetcher fetcher, Uri baseUri)
    {
        return new V8SpaEngine(fetcher, baseUri);
    }
}
