using Crawler.AngleSharp.Js;
using Jint.Runtime.Modules;
using Module = Jint.Runtime.Modules.Module;

namespace Crawler.AngleSharp.Js.Jint;

internal sealed class JintModuleLoader : IModuleLoader
{
    private readonly IModuleFetcher _fetcher;
    private readonly Uri _baseUri;

    public JintModuleLoader(IModuleFetcher fetcher, Uri baseUri)
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
        var source = _fetcher.Fetch(uri) ?? "export {};";
        return ModuleFactory.BuildSourceTextModule(engine, resolved, source);
    }
}
