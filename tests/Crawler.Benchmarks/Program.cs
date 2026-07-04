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
            var framework = args.Length >= 4 ? args[3] : "preact";
            await ProfileRunner.Run(combo, iterations, framework);
            return;
        }

        if (args.Length >= 1 && args[0] == "rendersize")
        {
            var combo = args.Length >= 2 ? args[1] : "jint-hap";
            var framework = args.Length >= 3 ? args[2] : "preact";
            await ProfileRunner.RenderSize(combo, framework);
            return;
        }

        new BenchmarkSwitcher(typeof(Program).Assembly).Run(args);
    }
}
