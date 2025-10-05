using BenchmarkDotNet.Running;

namespace Crawler.Benchmarks;

internal class Program
{
    static void Main(string[] args)
    {
        new BenchmarkSwitcher(typeof(Program).Assembly).Run(args);
    }
}
