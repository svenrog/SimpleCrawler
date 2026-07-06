using BenchmarkDotNet.Running;

namespace SimpleCrawler.Benchmarks;

internal class Program
{
    static void Main(string[] args)
    {
        new BenchmarkSwitcher(typeof(Program).Assembly).Run(args);
    }
}
