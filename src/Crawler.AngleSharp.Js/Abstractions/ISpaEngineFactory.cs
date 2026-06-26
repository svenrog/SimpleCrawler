namespace Crawler.AngleSharp.Js.Abstractions;

public interface ISpaEngineFactory
{
    ISpaEngine Create(IModuleFetcher fetcher, Uri baseUri);
}
