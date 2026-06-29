using Acornima.Ast;
using Jint;
using System.Collections.Concurrent;

namespace Crawler.Js.Jint;

internal sealed class JintScriptCache
{
    private readonly ConcurrentDictionary<string, Prepared<Script>> _scripts = new(StringComparer.Ordinal);

    public Prepared<Script> GetOrPrepare(string key, string source)
    {
        return _scripts.GetOrAdd(key, static (location, code) => Engine.PrepareScript(code, location), source);
    }
}
