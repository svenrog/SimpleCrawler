namespace Crawler.AngleSharp.Js.Abstractions;

public interface IJsEngineFactory
{
    IJsEngine Create(IModuleFetcher fetcher, Uri baseUri);
}
