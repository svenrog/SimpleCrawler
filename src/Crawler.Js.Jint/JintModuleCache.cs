using Acornima.Ast;
using Crawler.Js.Abstractions;
using Jint;
using System.Collections.Concurrent;

namespace Crawler.Js.Jint;

// Every crawled SPA page spins up a fresh Engine, but they all import the same module graph. Parsing
// is the dominant per-page cost and a Prepared<Module> is immutable and reusable across engines, so we
// parse each module once (keyed by absolute URL) and feed the cached form to every engine instead of
// re-parsing the source string each time. Lives one crawl; URLs are unique per crawled origin.
internal sealed class JintModuleCache
{
    private readonly ConcurrentDictionary<string, Prepared<Module>> _modules = new(StringComparer.Ordinal);

    public Prepared<Module> GetOrPrepare(string key, string source)
    {
        return _modules.GetOrAdd(key, static (location, code) => Engine.PrepareModule(code, location), source);
    }

    public Prepared<Module> GetOrPrepare(Uri uri, IModuleFetcher fetcher)
    {
        return _modules.GetOrAdd(uri.AbsoluteUri, static (location, state) =>
        {
            var source = state.fetcher.Fetch(state.uri) ?? "export {};";
            return Engine.PrepareModule(source, location);
        }, (uri, fetcher));
    }
}
