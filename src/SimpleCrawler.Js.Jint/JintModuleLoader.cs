using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Js.Abstractions;
using Jint.Runtime.Modules;
using Module = Jint.Runtime.Modules.Module;

namespace SimpleCrawler.Js.Jint;

internal sealed class JintModuleLoader : IModuleLoader
{
    private readonly JintModuleCache _cache;
    private readonly IModuleFetcher _fetcher;
    private readonly Uri _baseUri;

    public JintModuleLoader(JintModuleCache cache, IModuleFetcher fetcher, Uri baseUri)
    {
        _cache = cache;
        _fetcher = fetcher;
        _baseUri = baseUri;
    }

    public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
    {
        var uri = ModuleSpecifier.Resolve(
            moduleRequest.Specifier, ResolveReferrer(referencingModuleLocation), _fetcher.ImportMap);

        return new ResolvedSpecifier(moduleRequest, uri.AbsoluteUri, uri, SpecifierType.RelativeOrAbsolute);
    }

    /// <summary>
    /// Jint reports a registered entry module's location as the raw src path (e.g.
    /// "/assets/index.js"), so a relative referrer is resolved against the page origin.
    /// </summary>
    private Uri ResolveReferrer(string? location)
    {
        if (location == null)
            return _baseUri;

        var referrer = UriHelper.TryCreateHttpAbsolute(location, out var absolute) ? absolute : new Uri(_baseUri, location);
        return ModuleSpecifier.ReferrerOrBase(referrer, _baseUri);
    }

    public Module LoadModule(global::Jint.Engine engine, ResolvedSpecifier resolved)
    {
        var uri = resolved.Uri ?? new Uri(resolved.Key);
        var prepared = _cache.GetOrPrepare(uri, _fetcher);
        return ModuleFactory.BuildSourceTextModule(engine, in prepared);
    }
}
