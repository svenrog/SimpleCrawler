namespace Crawler.AngleSharp.Js;

public interface ISpaEngineFactory
{
    ISpaEngine Create(IModuleFetcher fetcher, Uri baseUri);
}
