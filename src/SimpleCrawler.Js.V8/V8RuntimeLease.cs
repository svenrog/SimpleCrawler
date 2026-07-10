using Microsoft.ClearScript.V8;

namespace SimpleCrawler.Js.V8;

/// <summary>
/// Pairs a pooled isolate with its page count so the pool can retire it after a fixed number of uses.
/// </summary>
internal sealed class V8RuntimeLease
{
    public V8RuntimeLease(V8Runtime runtime)
    {
        Runtime = runtime;
    }

    public V8Runtime Runtime { get; }

    public int Uses { get; set; }
}
