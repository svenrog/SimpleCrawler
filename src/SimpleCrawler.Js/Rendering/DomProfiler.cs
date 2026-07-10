using System.Collections.Concurrent;
using System.Text.Json;

namespace SimpleCrawler.Js.Rendering;

/// <summary>
/// Env-gated (JSRENDER_DOM_PROFILE=1) counter for the DOM operations a bundle drives during render. Unlike
/// RenderProfiler (which times engine-boundary calls), this reaches inside bundleExec: dom.js counts the
/// public DOM calls the bundle issues, the host reads the per-page tally and sums it here, and a table is
/// printed at process exit — so a heavy bundleExec can be attributed to "the bundle renders more" versus
/// "the interpreter is slow". Disabled by default, in which case nothing is embedded and no tally is read.
/// </summary>
internal static class DomProfiler
{
    public static readonly bool Enabled =
        Environment.GetEnvironmentVariable("JSRENDER_DOM_PROFILE") is "1" or "true";

    private static readonly ConcurrentDictionary<string, long> _counts = new();
    private static readonly ConcurrentDictionary<string, double> _times = new();
    private static long _pages;
    private static bool _hasTimes;

    static DomProfiler()
    {
        if (Enabled)
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Dump();
    }

    /// <summary>
    /// Per-page tally shape from __crawlerDomProfileDump(): { counts: {op:n}, timesMs: {op:ms} | null }.
    /// timesMs is null unless the engine exposed a high-res clock (V8 under profiling); Jint reports counts only.
    /// </summary>
    public static void Add(string json)
    {
        if (!Enabled || string.IsNullOrEmpty(json))
            return;

        Interlocked.Increment(ref _pages);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        foreach (var op in root.GetProperty("counts").EnumerateObject())
            _counts.AddOrUpdate(op.Name, op.Value.GetInt64(), (_, existing) => existing + op.Value.GetInt64());

        if (root.TryGetProperty("timesMs", out var timesMs) && timesMs.ValueKind == JsonValueKind.Object)
        {
            _hasTimes = true;
            foreach (var op in timesMs.EnumerateObject())
                _times.AddOrUpdate(op.Name, op.Value.GetDouble(), (_, existing) => existing + op.Value.GetDouble());
        }
    }

    public static void Dump()
    {
        if (_counts.IsEmpty)
            return;

        var pages = Math.Max(_pages, 1);
        var rows = _counts
            .Select(kv => (Op: kv.Key, Total: kv.Value, PerPage: kv.Value / (double)pages, Ms: _times.GetValueOrDefault(kv.Key)))
            .OrderByDescending(r => _hasTimes ? r.Ms : r.Total)
            .ToList();

        Console.WriteLine();
        Console.WriteLine($"=== JS DOM op profile ({_pages} pages, summed over threads) ===");
        if (_hasTimes)
        {
            var totalMs = _times.Values.Sum();
            Console.WriteLine($"{"op",-28} {"total",12} {"per page",10} {"self ms",12} {"us/op",10} {"%time",7}");
            foreach (var (Op, Total, PerPage, Ms) in rows)
                Console.WriteLine($"{Op,-28} {Total,12} {PerPage,10:F1} {Ms,12:F1} {Ms * 1000.0 / Total,10:F2} {(totalMs > 0 ? Ms / totalMs * 100 : 0),6:F1}%");
            Console.WriteLine($"{"(total self ms)",-28} {"",12} {"",10} {totalMs,12:F1}");
        }
        else
        {
            Console.WriteLine($"{"op",-28} {"total",14} {"per page",12}");
            foreach (var (Op, Total, PerPage, _) in rows)
                Console.WriteLine($"{Op,-28} {Total,14} {PerPage,12:F1}");
        }
        Console.WriteLine("=================================================================");
    }
}
