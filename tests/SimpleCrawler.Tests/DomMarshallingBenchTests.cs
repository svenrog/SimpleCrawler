using System.Diagnostics;
using Jint;
using Microsoft.ClearScript.V8;

namespace SimpleCrawler.Tests;

/// <summary>
/// Measures the per-access cost of crossing the JS&lt;-&gt;host boundary for a POCO host object, which is the
/// cost a JS-implemented DOM would remove. Gated behind DOM_BENCH=1 so it never runs in a normal pass.
/// Reports ns/op for scalar get, method call, mutating call, host-object get (no alloc), and host-object
/// returning call (alloc), on both engines. Run:
///   DOM_BENCH=1 dotnet run --project tests/SimpleCrawler.Tests -c Release -- -method "*MeasuresPerAccessCost"
/// </summary>
public class DomMarshallingBenchTests
{
    public sealed class BenchNode
    {
        private readonly BenchNode _child;

        public BenchNode() => _child = this;

        public string name => "div";
        public string getAttr(string key) => key;

#pragma warning disable IDE0060 // args exist only to be marshaled across the JS boundary being measured
        public void setAttr(string key, object? value) { }
#pragma warning restore IDE0060
        public BenchNode child => _child;
        public BenchNode make() => new();
    }

    private const int _v8Iters = 2_000_000;
    private const int _jintIters = 200_000;

    [Fact]
    public void MeasuresPerAccessCost()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("DOM_BENCH") is "1", "Set DOM_BENCH=1 to run.");

        Console.WriteLine();
        Console.WriteLine($"{"op",-22}{"V8 ns/op",14}{"Jint ns/op",14}");
        foreach (var (label, body) in Cases())
            Console.WriteLine($"{label,-22}{V8(body, _v8Iters),14:F1}{Jint(body, _jintIters),14:F1}");
    }

    private static IEnumerable<(string Label, string Body)> Cases()
    {
        yield return ("scalar get", "s=n.name;");
        yield return ("method->string", "s=n.getAttr('id');");
        yield return ("method mutate", "n.setAttr('id',i);");
        yield return ("host get (no alloc)", "c=n.child;");
        yield return ("host get (alloc)", "c=n.make();");
    }

    private static double V8(string body, int iters)
    {
        using var engine = new V8ScriptEngine();
        engine.AddHostObject("n", new BenchNode());
        var loop = $"(function(){{var s,c;for(var i=0;i<{iters};i++){{{body}}}}})()";
        var empty = $"(function(){{var s,c;for(var i=0;i<{iters};i++){{}}}})()";

        engine.Execute(loop);
        var baseline = Time(() => engine.Execute(empty));
        var measured = Time(() => engine.Execute(loop));
        return (measured - baseline) / iters * 1_000_000.0;
    }

    private static double Jint(string body, int iters)
    {
        var engine = new Engine();
        engine.SetValue("n", new BenchNode());
        var loop = $"(function(){{var s,c;for(var i=0;i<{iters};i++){{{body}}}}})()";
        var empty = $"(function(){{var s,c;for(var i=0;i<{iters};i++){{}}}})()";

        engine.Execute(loop);
        var baseline = Time(() => engine.Execute(empty));
        var measured = Time(() => engine.Execute(loop));
        return (measured - baseline) / iters * 1_000_000.0;
    }

    private static double Time(Action action)
    {
        var best = double.MaxValue;
        for (var run = 0; run < 5; run++)
        {
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            best = Math.Min(best, sw.Elapsed.TotalMilliseconds);
        }

        return best;
    }
}
