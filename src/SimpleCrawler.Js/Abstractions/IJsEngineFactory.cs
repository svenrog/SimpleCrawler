namespace SimpleCrawler.Js.Abstractions;

public interface IJsEngineFactory
{
    /// <summary>
    /// Creates an engine for one page. <paramref name="cancellationToken"/> is applied at construction
    /// because page scripts run synchronously on the calling thread: an engine has to be able to stop
    /// itself, so a token observed only at the renderer's <c>await</c> points reaches a running page never.
    /// <para>
    /// The wall-clock ceiling that bounds a page which is not cancelled but never returns is <em>not</em>
    /// here — it lives on each engine's own options, because what it can be measured over differs by what
    /// the engine can enforce. See <c>JintEngineOptions</c> and <c>V8EngineOptions</c>.
    /// </para>
    /// </summary>
    IJsEngine Create(IModuleFetcher fetcher, Uri baseUri, CancellationToken cancellationToken);
}
