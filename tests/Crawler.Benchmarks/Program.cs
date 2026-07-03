using BenchmarkDotNet.Running;

namespace Crawler.Benchmarks;

internal class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length >= 1 && args[0] == "profile")
        {
            var combo = args.Length >= 2 ? args[1] : "jint-hap";
            var iterations = args.Length >= 3 ? int.Parse(args[2]) : 20;
            // Optional Jint engine-pool cap for A/B runs (0 = fresh engine per page); omitted = production default.
            int? jintMaxUses = args.Length >= 4 ? int.Parse(args[3]) : null;
            await ProfileRunner.Run(combo, iterations, jintMaxUses);
            return;
        }

        new BenchmarkSwitcher(typeof(Program).Assembly).Run(args);
    }
}
