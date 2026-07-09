using Acornima.Ast;
using SimpleCrawler.Js.Abstractions;
using SimpleCrawler.Js.Services;
using Jint;

namespace SimpleCrawler.Js.Jint;

// Every crawled SPA page spins up a fresh Engine, but they all import the same module graph. Parsing
// is the dominant per-page cost and a Prepared<Module> is immutable and reusable across engines, so we
// parse each module once (keyed by absolute URL) and feed the cached form to every engine instead of
// re-parsing the source string each time. Bounded by an LRU cap so a multi-bundle site can't retain a
// distinct module AST per URL for the whole crawl; inline (per-page) modules are never cached at all.
internal sealed class JintModuleCache
{
    private const int _capacity = 512;
    private const string _emptyModule = "export {};";

    private readonly BoundedLruCache<string, Prepared<Module>> _modules = new(_capacity);

    public Prepared<Module> GetOrPrepare(string key, string source)
    {
        return _modules.GetOrAdd(key, source, static (location, code) => PrepareOrEmpty(code, location));
    }

    public Prepared<Module> GetOrPrepare(Uri uri, IModuleFetcher fetcher)
    {
        return _modules.GetOrAdd(uri.AbsoluteUri, (uri, fetcher), static (location, state) =>
            PrepareOrEmpty(state.fetcher.Fetch(state.uri) ?? _emptyModule, location));
    }

    // A fetched module URL that returns the site's HTML catch-all (or any non-JS/malformed source) can't be
    // parsed as a module — Jint throws ScriptPreparationException from inside the loader, which would abort the
    // whole importing page. Degrade to an empty module so one unresolvable dependency doesn't take the rest of
    // the module graph down with it.
    private static Prepared<Module> PrepareOrEmpty(string source, string location)
    {
        try
        {
            return Engine.PrepareModule(source, location);
        }
        catch (ScriptPreparationException)
        {
            return Engine.PrepareModule(_emptyModule, location);
        }
    }
}
