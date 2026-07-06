using System.Collections.Concurrent;
using System.Diagnostics;

namespace SimpleCrawler.Js.Rendering;

// Env-gated profiler (JSRENDER_PROFILE=1) that sums Stopwatch ticks and call counts per bucket across all
// pages/threads and prints a table at process exit. Every entry point early-returns when disabled, and Scope
// hands back a cached no-op, so the profiler adds no timestamp read or allocation to the production hot path.
internal static class RenderProfiler
{
    public static readonly bool Enabled =
        Environment.GetEnvironmentVariable("JSRENDER_PROFILE") is "1" or "true";

    private static readonly ConcurrentDictionary<string, Bucket> _buckets = new();

    static RenderProfiler()
    {
        if (Enabled)
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Dump();
    }

    // No-allocation point timer for per-call hot paths (ProfilingJsEngine): a long stamp, stopped explicitly,
    // so measuring a boundary call costs nothing on the heap.
    public static long Start() => Enabled ? Stopwatch.GetTimestamp() : 0;

    public static void Stop(string bucket, long start)
    {
        if (!Enabled)
            return;

        var entry = _buckets.GetOrAdd(bucket, static _ => new Bucket());
        Interlocked.Add(ref entry.Ticks, Stopwatch.GetTimestamp() - start);
        Interlocked.Increment(ref entry.Count);
    }

    public static void Dump()
    {
        if (_buckets.IsEmpty)
            return;

        var rows = _buckets
            .Select(kv => (Name: kv.Key, Ms: kv.Value.Ticks * 1000.0 / Stopwatch.Frequency, kv.Value.Count))
            .OrderByDescending(r => r.Ms)
            .ToList();

        Console.WriteLine();
        Console.WriteLine("=== JS render profile (total ms across all pages, summed over threads) ===");
        Console.WriteLine($"{"bucket",-22} {"total ms",12} {"calls",10} {"us/call",12}");
        foreach (var (Name, Ms, Count) in rows)
        {
            Console.WriteLine($"{Name,-22} {Ms,12:F1} {Count,10} {Ms * 1000.0 / Count,12:F2}");
        }
        Console.WriteLine("=========================================================================");
    }

    private sealed class Bucket
    {
        public long Ticks;
        public long Count;
    }
}
