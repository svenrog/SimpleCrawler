using Crawler.Js.Abstractions;
using Jint.Runtime.Modules;
using Module = Jint.Runtime.Modules.Module;

namespace Crawler.Js.Jint;

internal sealed class JintModuleLoader : IModuleLoader
{
    private readonly JintModuleCache _cache;
    private IModuleFetcher _fetcher = null!;
    private Uri _baseUri = null!;

    public JintModuleLoader(JintModuleCache cache)
    {
        _cache = cache;
    }

    // The loader outlives a single page when its engine is pooled, so the per-page fetcher (page HttpClient +
    // cancellation) and base URI are rebound before each page rather than fixed at construction.
    public void Rebind(IModuleFetcher fetcher, Uri baseUri)
    {
        _fetcher = fetcher;
        _baseUri = baseUri;
    }

    public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
    {
        var specifier = moduleRequest.Specifier;

        Uri uri;
        if (Uri.TryCreate(specifier, UriKind.Absolute, out var absolute))
            uri = absolute;
        else
            uri = new Uri(ResolveReferrer(referencingModuleLocation), specifier);

        return new ResolvedSpecifier(moduleRequest, uri.AbsoluteUri, uri, SpecifierType.RelativeOrAbsolute);
    }

    // Jint reports a registered entry module's location as the raw src path (e.g.
    // "/assets/index.js"), so a relative referrer is resolved against the page origin.
    private Uri ResolveReferrer(string? location)
    {
        if (location == null)
            return _baseUri;

        return Uri.TryCreate(location, UriKind.Absolute, out var absolute) ? absolute : new Uri(_baseUri, location);
    }

    public Module LoadModule(global::Jint.Engine engine, ResolvedSpecifier resolved)
    {
        var uri = resolved.Uri ?? new Uri(resolved.Key);
        var prepared = _cache.GetOrPrepare(uri, _fetcher);
        return ModuleFactory.BuildSourceTextModule(engine, in prepared);
    }
}
