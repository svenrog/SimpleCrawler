using SimpleCrawler.Js.Abstractions;
using Microsoft.Extensions.Options;

namespace SimpleCrawler.Js.Jint;

internal sealed class JintJsEngineFactory : IJsEngineFactory
{
    private readonly JintModuleCache _moduleCache = new();
    private readonly JintScriptCache _scriptCache = new();
    private readonly JintEngineOptions _options;

    public JintJsEngineFactory(IOptions<JintEngineOptions> options)
    {
        _options = options.Value;
    }

    public IJsEngine Create(IModuleFetcher fetcher, Uri baseUri, CancellationToken cancellationToken)
    {
        return new JintJsEngine(_moduleCache, _scriptCache, fetcher, baseUri, _options, cancellationToken);
    }
}
