using Acornima.Ast;
using Crawler.Js.Abstractions;
using Crawler.Js.Services;
using Jint;

namespace Crawler.Js.Jint;

// Every crawled SPA page spins up a fresh Engine, but they all import the same module graph. Parsing
// is the dominant per-page cost and a Prepared<Module> is immutable and reusable across engines, so we
// parse each module once (keyed by absolute URL) and feed the cached form to every engine instead of
// re-parsing the source string each time. Bounded by an LRU cap so a multi-bundle site can't retain a
// distinct module AST per URL for the whole crawl; inline (per-page) modules are never cached at all.
internal sealed class JintModuleCache
{
    private const int _capacity = 512;

    private readonly BoundedLruCache<string, Prepared<Module>> _modules = new(_capacity);

    public Prepared<Module> GetOrPrepare(string key, string source)
    {
        return _modules.GetOrAdd(key, source, static (location, code) => Engine.PrepareModule(code, location));
    }

    public Prepared<Module> GetOrPrepare(Uri uri, IModuleFetcher fetcher)
    {
        return _modules.GetOrAdd(uri.AbsoluteUri, (uri, fetcher), static (location, state) =>
        {
            var source = state.fetcher.Fetch(state.uri) ?? "export {};";
            return Engine.PrepareModule(source, location);
        });
    }
}
