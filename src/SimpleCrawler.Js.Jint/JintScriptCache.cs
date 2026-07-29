using Acornima.Ast;
using SimpleCrawler.Js.Services;
using Jint;

namespace SimpleCrawler.Js.Jint;

/// <summary>
/// Parsed forms of external scripts (stable URLs), reused across the fresh per-page engines. Bounded by an
/// LRU cap: a heterogeneous site yields thousands of distinct chunk URLs whose ASTs would otherwise be held
/// for the whole crawl, so the hot shared bundle stays resident while one-off chunks are evicted.
/// </summary>
internal sealed class JintScriptCache
{
    private const int _capacity = 512;

    /// <summary>
    /// A prepared script is parsed outside the engine, so it does not inherit the engine's
    /// <c>RetainFunctionSourceText</c> and would answer <c>Function.prototype.toString()</c> with a
    /// <c>[native code]</c> placeholder — for every function an external bundle defines, while an inline
    /// script kept its source. A bundle that reads its own source then takes its tampered-with branch: an
    /// anti-bot payload's self-defence check compares a function against its expected text and, on failing
    /// it, enters a loop that never terminates.
    /// </summary>
    private static readonly ScriptPreparationOptions _preparationOptions =
        new() { ParsingOptions = ScriptParsingOptions.Default with { RetainFunctionSourceText = true } };

    private readonly BoundedLruCache<string, Prepared<Script>> _scripts = new(_capacity);

    public Prepared<Script> GetOrPrepare(string key, string source)
    {
        return _scripts.GetOrAdd(key, source,
            static (location, code) => Engine.PrepareScript(code, location, options: _preparationOptions));
    }
}
