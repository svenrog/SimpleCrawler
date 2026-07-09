using Acornima.Ast;
using SimpleCrawler.Js.Services;
using Jint;

namespace SimpleCrawler.Js.Jint;

// Parsed forms of external scripts (stable URLs), reused across the fresh per-page engines. Bounded by an
// LRU cap: a heterogeneous site yields thousands of distinct chunk URLs whose ASTs would otherwise be held
// for the whole crawl, so the hot shared bundle stays resident while one-off chunks are evicted.
internal sealed class JintScriptCache
{
    private const int _capacity = 512;

    private readonly BoundedLruCache<string, Prepared<Script>> _scripts = new(_capacity);

    public Prepared<Script> GetOrPrepare(string key, string source)
    {
        return _scripts.GetOrAdd(key, source, static (location, code) => Engine.PrepareScript(code, location));
    }
}
